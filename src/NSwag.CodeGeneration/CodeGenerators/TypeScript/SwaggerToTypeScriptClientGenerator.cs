﻿//-----------------------------------------------------------------------
// <copyright file="SwaggerToTypeScriptClientGenerator.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/NSwag/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using NJsonSchema;
using NJsonSchema.CodeGeneration.TypeScript;
using NSwag.CodeGeneration.CodeGenerators.Models;
using NSwag.CodeGeneration.CodeGenerators.TypeScript.Models;
using NSwag.CodeGeneration.CodeGenerators.TypeScript.Templates;

namespace NSwag.CodeGeneration.CodeGenerators.TypeScript
{
    /// <summary>Generates the CSharp service client code. </summary>
    public class SwaggerToTypeScriptClientGenerator : ClientGeneratorBase
    {
        private readonly SwaggerService _service;
        private readonly TypeScriptTypeResolver _resolver;

        /// <summary>Initializes a new instance of the <see cref="SwaggerToTypeScriptClientGenerator" /> class.</summary>
        /// <param name="service">The service.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="System.ArgumentNullException">service</exception>
        /// <exception cref="ArgumentNullException"><paramref name="service" /> is <see langword="null" />.</exception>
        public SwaggerToTypeScriptClientGenerator(SwaggerService service, SwaggerToTypeScriptClientGeneratorSettings settings)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            Settings = settings;

            _service = service;
            foreach (var definition in _service.Definitions.Where(p => string.IsNullOrEmpty(p.Value.TypeNameRaw)))
                definition.Value.TypeNameRaw = definition.Key;

            _resolver = new TypeScriptTypeResolver(_service.Definitions.Select(p => p.Value).ToArray(), Settings.TypeScriptGeneratorSettings);
        }

        /// <summary>Gets or sets the generator settings.</summary>
        public SwaggerToTypeScriptClientGeneratorSettings Settings { get; set; }

        /// <summary>Gets the language.</summary>
        protected override string Language => "TypeScript";

        /// <summary>Generates the file.</summary>
        /// <returns>The file contents.</returns>
        public override string GenerateFile()
        {
            return GenerateFile(_service, _resolver);
        }

        internal override ClientGeneratorBaseSettings BaseSettings => Settings;

        internal override string RenderFile(string clientCode, string[] clientClasses)
        {
            var template = new FileTemplate();
            template.Initialize(new
            {
                Toolchain = SwaggerService.ToolchainVersion,
                IsAngular2 = Settings.GenerateClientClasses && Settings.Template == TypeScriptTemplate.Angular2,

                Clients = Settings.GenerateClientClasses ? clientCode : string.Empty,
                Types = GenerateDtoTypes(),

                ExtensionCodeBefore = Settings.TypeScriptGeneratorSettings.ProcessedExtensionCode.CodeBefore, 
                ExtensionCodeAfter = GenerateExtensionCodeAfter(clientClasses),

                HasModuleName = !string.IsNullOrEmpty(Settings.TypeScriptGeneratorSettings.ModuleName),
                ModuleName = Settings.TypeScriptGeneratorSettings.ModuleName
            });
            return template.Render();
        }

        internal override string RenderClientCode(string controllerName, IList<OperationModel> operations)
        {
            UpdateUseDtoClassAndDataConversionCodeProperties(operations);

            var template = Settings.CreateTemplate();
            template.Initialize(new ClientTemplateModel(GetClassName(controllerName), operations, _service, Settings));
            var code = template.Render();

            return AppendExtensionClassIfNecessary(controllerName, code);
        }

        private string AppendExtensionClassIfNecessary(string controllerName, string code)
        {
            if (Settings.TypeScriptGeneratorSettings.ExtendedClasses?.Contains(controllerName) == true)
            {
                var extensionCode = Settings.TypeScriptGeneratorSettings.ProcessedExtensionCode;
                return extensionCode.Classes.ContainsKey(controllerName)
                    ? code + "\n\n" + extensionCode.Classes[controllerName]
                    : code;
            }
            return code;
        }

        internal override string GetExceptionType(SwaggerOperation operation)
        {
            if (operation.Responses.Count(r => !HttpUtilities.IsSuccessStatusCode(r.Key)) == 0)
                return "string";

            return string.Join(" | ", operation.Responses
                .Where(r => !HttpUtilities.IsSuccessStatusCode(r.Key) && r.Value.Schema != null)
                .Select(r => GetType(r.Value.ActualResponseSchema, r.Value.IsNullable(PropertyNullHandling.Required), "Exception"))
                .Concat(new[] { "string" }));
        }

        internal override string GetResultType(SwaggerOperation operation)
        {
            var response = GetSuccessResponse(operation);
            if (response?.Schema == null)
                return "void";

            return GetType(response.ActualResponseSchema, response.IsNullable(PropertyNullHandling.Required), "Response");
        }

        internal override string GetType(JsonSchema4 schema, bool isNullable, string typeNameHint)
        {
            if (schema == null)
                return "void";

            if (schema.ActualSchema.IsAnyType || schema.ActualSchema.Type == JsonObjectType.File)
                return "any";

            return _resolver.Resolve(schema.ActualSchema, isNullable, typeNameHint);
        }

        private string GetClassName(string className)
        {
            if (Settings.TypeScriptGeneratorSettings.ExtendedClasses?.Contains(className) == true)
                return className + "Base";

            return className;
        }

        private string GenerateExtensionCodeAfter(string[] clientClasses)
        {
            var clientClassesVariable = "{" + string.Join(", ", clientClasses.Select(c => "'" + c + "': " + c)) + "}";
            return Settings.TypeScriptGeneratorSettings.ProcessedExtensionCode.CodeAfter.Replace("{clientClasses}", clientClassesVariable);
        }

        private string GenerateDtoTypes()
        {
            return Settings.GenerateDtoTypes ? _resolver.GenerateTypes(Settings.TypeScriptGeneratorSettings.ProcessedExtensionCode) : string.Empty;
        }

        private void UpdateUseDtoClassAndDataConversionCodeProperties(IEnumerable<OperationModel> operations)
        {
            foreach (var operation in operations)
            {
                foreach (var parameter in operation.Parameters)
                {
                    if (parameter.IsDictionary)
                    {
                        if (parameter.Schema.AdditionalPropertiesSchema != null)
                        {
                            var itemTypeName = _resolver.Resolve(parameter.Schema.AdditionalPropertiesSchema, false, string.Empty);
                            parameter.UseDtoClass = Settings.TypeScriptGeneratorSettings.GetTypeStyle(itemTypeName) != TypeScriptTypeStyle.Interface &&
                                _resolver.HasTypeGenerator(itemTypeName);
                        }
                    }
                    else if (parameter.IsArray)
                    {
                        if (parameter.Schema.Item != null)
                        {
                            var itemTypeName = _resolver.Resolve(parameter.Schema.Item, false, string.Empty);
                            parameter.UseDtoClass = Settings.TypeScriptGeneratorSettings.GetTypeStyle(itemTypeName) != TypeScriptTypeStyle.Interface &&
                                _resolver.HasTypeGenerator(itemTypeName);
                        }
                    }
                    else
                        parameter.UseDtoClass = Settings.TypeScriptGeneratorSettings.GetTypeStyle(parameter.Type) != TypeScriptTypeStyle.Interface &&
                            _resolver.HasTypeGenerator(parameter.Type);
                }

                foreach (var response in operation.Responses.Where(r => r.HasType))
                {
                    response.UseDtoClass = Settings.TypeScriptGeneratorSettings.GetTypeStyle(response.Type) != TypeScriptTypeStyle.Interface;
                    response.DataConversionCode = DataConversionGenerator.RenderConvertToClassCode(new DataConversionParameters
                    {
                        Variable = "result" + response.StatusCode,
                        Value = "resultData" + response.StatusCode,
                        Schema = response.ActualResponseSchema,
                        IsPropertyNullable = response.IsNullable,
                        TypeNameHint = string.Empty,
                        Resolver = _resolver
                    });
                }

                if (operation.HasDefaultResponse && operation.DefaultResponse.HasType)
                {
                    operation.DefaultResponse.UseDtoClass = Settings.TypeScriptGeneratorSettings.GetTypeStyle(operation.DefaultResponse.Type) != TypeScriptTypeStyle.Interface;
                    operation.DefaultResponse.DataConversionCode = DataConversionGenerator.RenderConvertToClassCode(new DataConversionParameters
                    {
                        Variable = "result",
                        Value = "resultData",
                        Schema = operation.DefaultResponse.ActualResponseSchema,
                        IsPropertyNullable = operation.DefaultResponse.IsNullable,
                        TypeNameHint = string.Empty,
                        Resolver = _resolver
                    });
                }
            }
        }
    }
}
