using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using ARDB = Autodesk.Revit.DB;
using OS = System.Environment;

namespace RhinoInside.Revit.GH.Components.Families
{
  using External.DB.Extensions;

  [ComponentVersion(introduced: "1.37")]
  public class FamilyFile : ZuiComponent
  {
    public override Guid ComponentGuid => new Guid("7E3C9A1B-4F2D-4A8E-9C6B-1D5E8F0A2B3C");
    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override string IconTag => "F";

    public FamilyFile() : base
    (
      name: "New Family File",
      nickname: "RFA",
      description: "Creates a Revit family file (.rfa) from a template, or renames/moves a previously tracked file when Name or Folder change.",
      category: "Revit",
      subCategory: "Component"
    )
    { }

    protected override ParamDefinition[] Inputs => inputs;
    static readonly ParamDefinition[] inputs =
    {
      new ParamDefinition
      (
        new Param_String()
        {
          Name = "Name",
          NickName = "N",
          Description = "Family file name",
        }
      ),
      new ParamDefinition
      (
        new Param_FilePath()
        {
          Name = "Template",
          NickName = "T",
          Description = "Family template (.rft). Used only when creating a new file.",
          FileFilter = "Family Template Files (*.rft)|*.rft",
          Optional = true
        }
      ),
      new ParamDefinition
      (
        new Param_FilePath()
        {
          Name = "Folder",
          NickName = "F",
          Description = "Absolute folder where the family file is stored.",
        }
      ),
      new ParamDefinition
      (
        new Param_Boolean()
        {
          Name = "Overwrite",
          NickName = "O",
          Description = "Allow replacing an existing file at the destination when renaming/moving or creating.",
        }.SetDefaultVale(false),
        ParamRelevance.Secondary
      ),
    };

    protected override ParamDefinition[] Outputs => outputs;
    static readonly ParamDefinition[] outputs =
    {
      new ParamDefinition
      (
        new Param_String()
        {
          Name = "Path",
          NickName = "P",
          Description = "Absolute path to the tracked family file.",
        }
      ),
    };

    #region Tracking
    string TrackedPath { get; set; }

    void ClearTracking()
    {
      TrackedPath = null;
    }

    static bool PathsEqual(string a, string b) =>
      !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && PathExtension.Comparer.Equals(a, b);
    #endregion

    protected override void TrySolveInstance(IGH_DataAccess DA)
    {
      if (!Params.GetData(DA, "Name", out string name) || string.IsNullOrWhiteSpace(name)) return;
      if (!Params.TryGetData(DA, "Template", out string templatePath)) return;
      if (!Params.GetData(DA, "Folder", out string folder) || string.IsNullOrWhiteSpace(folder)) return;
      if (!Params.TryGetData(DA, "Overwrite", out bool? overwrite)) return;
      overwrite ??= false;

      name = name.Trim();
      foreach (var invalid in Path.GetInvalidFileNameChars())
      {
        if (name.IndexOf(invalid) >= 0)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Name contains invalid file name character '{invalid}'.");
          return;
        }
      }

      if (!Path.HasExtension(name))
        name += ".rfa";
      else if (!string.Equals(Path.GetExtension(name), ".rfa", StringComparison.OrdinalIgnoreCase))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name must use the .rfa extension.");
        return;
      }

      folder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      if (!PathExtension.TryGetFullyQualifiedPath(ref folder))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Folder should be an absolute path.");
        return;
      }

      var targetPath = Path.Combine(folder, name);
      if (!PathExtension.TryGetFullyQualifiedPath(ref targetPath))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not resolve target family file path.");
        return;
      }

      // Drop stale tracking if the previous file was deleted externally.
      if (!string.IsNullOrEmpty(TrackedPath) && !File.Exists(TrackedPath))
        ClearTracking();

      try
      {
        if (!string.IsNullOrEmpty(TrackedPath) && File.Exists(TrackedPath))
        {
          if (PathsEqual(TrackedPath, targetPath))
          {
            Params.TrySetData(DA, "Path", () => TrackedPath);
            return;
          }

          if (!TryMoveTrackedFile(TrackedPath, targetPath, overwrite.Value))
            return;

          TrackedPath = targetPath;
          Params.TrySetData(DA, "Path", () => TrackedPath);
          return;
        }

        if (File.Exists(targetPath))
        {
          TrackedPath = targetPath;
          AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Adopting existing family file '{targetPath.TripleDotPath(64)}'.");
          Params.TrySetData(DA, "Path", () => TrackedPath);
          return;
        }

        if (!TryCreateFamilyFile(targetPath, templatePath, overwrite.Value))
          return;

        TrackedPath = targetPath;
        Params.TrySetData(DA, "Path", () => TrackedPath);
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
      }
    }

    bool TryMoveTrackedFile(string sourcePath, string destinationPath, bool overwrite)
    {
      if (File.Exists(destinationPath))
      {
        if (!overwrite)
        {
          AddRuntimeMessage
          (
            GH_RuntimeMessageLevel.Error,
            $"Destination '{destinationPath.TripleDotPath(64)}' already exists. Enable Overwrite to replace it."
          );
          return false;
        }

        File.Delete(destinationPath);
      }

      var destinationFolder = Path.GetDirectoryName(destinationPath);
      if (!string.IsNullOrEmpty(destinationFolder))
        Directory.CreateDirectory(destinationFolder);

      File.Move(sourcePath, destinationPath);
      AddRuntimeMessage
      (
        GH_RuntimeMessageLevel.Remark,
        $"Moved family file to '{destinationPath.TripleDotPath(64)}'."
      );
      return true;
    }

    bool TryCreateFamilyFile(string targetPath, string templatePath, bool overwrite)
    {
      if (File.Exists(targetPath))
      {
        if (!overwrite)
        {
          AddRuntimeMessage
          (
            GH_RuntimeMessageLevel.Error,
            $"File '{targetPath.TripleDotPath(64)}' already exists. Enable Overwrite to replace it."
          );
          return false;
        }
      }

      if (string.IsNullOrWhiteSpace(templatePath))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Template is required to create a new family file.");
        return false;
      }

      if (!Path.HasExtension(templatePath))
        templatePath += ".rft";

      if (!FindTemplatePath(ref templatePath, out var pathWasRelative))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to find Template file '{templatePath}'.");
        return false;
      }

      if (pathWasRelative)
        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Using template file from '{templatePath}'");

      var destinationFolder = Path.GetDirectoryName(targetPath);
      if (!string.IsNullOrEmpty(destinationFolder))
        Directory.CreateDirectory(destinationFolder);

      using (Guest.Instance.PauseTransactionGroups())
      {
        ARDB.Document familyDoc = null;
        try
        {
          familyDoc = Revit.ActiveDBApplication.NewFamilyDocument(templatePath);
          if (familyDoc is null)
          {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to create family document from template.");
            return false;
          }

          using (var saveAsOptions = new ARDB.SaveAsOptions() { OverwriteExistingFile = overwrite })
            familyDoc.SaveAs(targetPath, saveAsOptions);
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException e)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
          return false;
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException e)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
          return false;
        }
        finally
        {
          familyDoc?.Release();
        }
      }

      if (!File.Exists(targetPath))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Family file was not written to disk.");
        return false;
      }

      return true;
    }

    static bool FindTemplatePath(ref string templatePath, out bool pathWasRelative)
    {
      pathWasRelative = !PathExtension.TryGetFullyQualifiedPath(ref templatePath);

      foreach (var invalid in Path.GetInvalidPathChars())
      {
        if (templatePath.Contains(invalid))
          return false;
      }

      var components = templatePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      foreach (var component in components.Skip(pathWasRelative ? 0 : 1))
      {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
          if (component.Contains(invalid))
            return false;
        }
      }

      if (!pathWasRelative)
        return File.Exists(templatePath);

      var folders = new List<string>();
      var app = Revit.ActiveDBApplication;
      if (app is object)
      {
        if (Directory.Exists(app.FamilyTemplatePath))
          folders.Add(app.FamilyTemplatePath);

        var defaultFamilyTemplatePath = Path.Combine
        (
          OS.GetFolderPath(OS.SpecialFolder.CommonApplicationData),
          "Autodesk",
          $"RVT {app.VersionNumber}",
          "Family Templates"
        );

        if (!folders.Contains(defaultFamilyTemplatePath) && Directory.Exists(defaultFamilyTemplatePath))
          folders.Add(defaultFamilyTemplatePath);
      }

      foreach (var folder in folders)
      {
        var template = components.Length == 1 ?
          Directory.EnumerateFiles(folder, components[0], SearchOption.TopDirectoryOnly).FirstOrDefault() :
          Path.GetFullPath(Path.Combine(folder, templatePath));

        if (File.Exists(template))
        {
          templatePath = template;
          return true;
        }
      }

      return false;
    }

    #region UI
    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
    {
      base.AppendAdditionalComponentMenuItems(menu);

      Menu_AppendItem
      (
        menu,
        "Release Family File",
        (sender, arg) =>
        {
          RecordUndoEvent("Release Family File");
          ClearTracking();
          ExpireSolution(true);
        },
        enabled: !string.IsNullOrEmpty(TrackedPath),
        false
      );
    }
    #endregion

    #region IO
    public override bool Read(GH_IReader reader)
    {
      if (!base.Read(reader))
        return false;

      string trackedPath = null;
      reader.TryGetString("TrackedPath", ref trackedPath);
      TrackedPath = string.IsNullOrWhiteSpace(trackedPath) ? null : trackedPath;

      return true;
    }

    public override bool Write(GH_IWriter writer)
    {
      if (!base.Write(writer))
        return false;

      if (!string.IsNullOrEmpty(TrackedPath))
        writer.SetString("TrackedPath", TrackedPath);

      return true;
    }
    #endregion
  }
}
