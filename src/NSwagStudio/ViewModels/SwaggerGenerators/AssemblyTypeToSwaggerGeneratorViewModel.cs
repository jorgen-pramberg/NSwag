//-----------------------------------------------------------------------
// <copyright file="AssemblyTypeToSwaggerGeneratorViewModel.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/NSwag/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using MyToolkit.Command;
using NJsonSchema;
using NSwag.CodeGeneration.SwaggerGenerators;
using NSwag.Commands;

namespace NSwagStudio.ViewModels.SwaggerGenerators
{
    public class AssemblyTypeToSwaggerGeneratorViewModel : ViewModelBase
    {
        private string[] _allClassNames;
        private AssemblyTypeToSwaggerCommand _command = new AssemblyTypeToSwaggerCommand();

        /// <summary>Initializes a new instance of the <see cref="AssemblyTypeToSwaggerGeneratorViewModel"/> class.</summary>
        public AssemblyTypeToSwaggerGeneratorViewModel()
        {
            BrowseAssemblyCommand = new AsyncRelayCommand(BrowseAssembly);

            LoadAssemblyCommand = new AsyncRelayCommand(LoadAssemblyAsync, () => !string.IsNullOrEmpty(AssemblyPath));
            LoadAssemblyCommand.TryExecute();
        }

        /// <summary>Gets or sets the generator settings.</summary>
        public AssemblyTypeToSwaggerCommand Command
        {
            get { return _command; }
            set
            {
                if (Set(ref _command, value))
                {
                    RaiseAllPropertiesChanged();
                    LoadAssemblyAsync();
                }
            }
        }

        public string ReferencePaths
        {
            get
            {
                return Command.ReferencePaths != null ? string.Join(",", Command.ReferencePaths) : "";
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Command.ReferencePaths = value.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                else
                    Command.ReferencePaths = new string[] { };
                RaisePropertyChanged(() => ReferencePaths);
            }
        }

        /// <summary>Gets the async types. </summary>
        public EnumHandling[] EnumHandlings
        {
            get { return Enum.GetNames(typeof(EnumHandling)).Select(t => (EnumHandling)Enum.Parse(typeof(EnumHandling), t)).ToArray(); }
        }

        /// <summary>Gets or sets the command to browse for an assembly.</summary>
        public AsyncRelayCommand BrowseAssemblyCommand { get; set; }

        /// <summary>Gets or sets the command to load the types from an assembly.</summary>
        public AsyncRelayCommand LoadAssemblyCommand { get; set; }

        /// <summary>Gets or sets the assembly path. </summary>
        public string AssemblyPath
        {
            get { return Command.AssemblyPath; }
            set
            {
                Command.AssemblyPath = value;
                LoadAssemblyCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(() => AssemblyPath);
                RaisePropertyChanged(() => AssemblyName);
            }
        }

        /// <summary>Gets the name of the selected assembly.</summary>
        public string AssemblyName
        {
            get { return Path.GetFileName(AssemblyPath); }
        }

        /// <summary>Gets or sets the class name. </summary>
        public string ClassName
        {
            get
            {
                return Command.ClassNames != null && Command.ClassNames.Any() ? Command.ClassNames.First() : null;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Command.ClassNames = new[] { value };
                else
                    Command.ClassNames = new string[] { };
                RaisePropertyChanged(() => ClassName);

            }
        }

        /// <summary>Gets or sets the all class names. </summary>
        public string[] AllClassNames
        {
            get { return _allClassNames; }
            set { Set(ref _allClassNames, value); }
        }

        private async Task BrowseAssembly()
        {
            var dlg = new OpenFileDialog();
            dlg.DefaultExt = ".dll";
            dlg.Filter = ".NET Assemblies (.dll)|*.dll";
            if (dlg.ShowDialog() == true)
            {
                AssemblyPath = dlg.FileName;
                await LoadAssemblyAsync();
            }
        }

        private Task LoadAssemblyAsync()
        {
            return RunTaskAsync(async () =>
            {
                AllClassNames = await Task.Run(() =>
                {
                    var generator = new AssemblyTypeToSwaggerGenerator(Command.Settings);
                    return generator.GetClasses();
                });
                ClassName = AllClassNames.FirstOrDefault();
            });
        }

        public async Task<string> GenerateSwaggerAsync()
        {
            return await RunTaskAsync(async () => (await Command.RunAsync()).ToJson());
        }
    }
}