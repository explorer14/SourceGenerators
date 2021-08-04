using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DtoGenerators
{
    [Generator]
    public class MappedDtoGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var timer = Stopwatch.StartNew();
            context.Information("Starting source generation...");
            var targetTypeTracker = context.SyntaxContextReceiver as TargetTypeTracker;
            var codeBuilder = new StringBuilder();

            if (targetTypeTracker == null)
            {
                context.Error($"An instance of ISyntaxContextReceiver " +
                    $"must be registered to receive syntax notifications! Found {targetTypeTracker}");

                return;
            }

            foreach (var typeNode in targetTypeTracker.TypesNeedingDtoGening)
            {
                var typeNodeSymbol = context.Compilation
                    .GetSemanticModel(typeNode.SyntaxTree)
                    .GetDeclaredSymbol(typeNode);

                if (typeNodeSymbol == null)
                    context.Warning(
                        $"Type {typeNode.Identifier.ValueText} does not have a corresponding ISymbol! " +
                        $"Skipping it!");
                else
                {
                    var entityClassNamespace = typeNodeSymbol.ContainingNamespace?.ToDisplayString()
                    ?? "NoNamespace";

                    var generatedDtoClassName = $"{typeNodeSymbol.Name}Dto";

                    codeBuilder.AddDefaultNamespaces();
                    codeBuilder.AddAutoGenComment();
                    codeBuilder.StartDtoNamespace(entityClassNamespace);
                    codeBuilder.StartClass(generatedDtoClassName);

                    var allProperties = typeNode.Members.OfType<PropertyDeclarationSyntax>();

                    foreach (var property in allProperties)
                        codeBuilder.AppendLine($"\t\t{property.BuildDtoProperty(context.Compilation)}");

                    codeBuilder.EndClass();

                    codeBuilder.StartExtensionClass();
                    codeBuilder.StartExtensionMethod(
                        generatedDtoClassName, 
                        typeNode.Identifier.ValueText);                    

                    var propertiesWithCollectionTypes =
                        new List<(IPropertySymbol Symbol, PropertyDeclarationSyntax Syntax)>();

                    foreach (var pds in allProperties)
                    {
                        var symbol = context.Compilation
                                 .GetSemanticModel(pds.SyntaxTree)
                                 .GetDeclaredSymbol(pds);
                        var property = symbol as IPropertySymbol;
                        var propertyType = property.Type as INamedTypeSymbol;

                        if (propertyType?.IsGenericType ?? false)
                        {
                            if (property.IsAtleastOneTypeArgumentCustomType())
                            {
                                codeBuilder.AddDtoConvertedPropertyMapping(property.Name);
                                propertiesWithCollectionTypes.Add((property, pds));
                            }
                            else
                                codeBuilder.AddDirectPropertyMapping(property.Name);
                        }
                        else
                        {
                            if (property.Type.TypeKind == TypeKind.Array)
                            {
                                var elementTypeSymbol = property.Type as IArrayTypeSymbol;

                                if ((elementTypeSymbol.ElementType.IsClass() ||
                                    elementTypeSymbol.ElementType.IsStruct()) &&
                                    property.IsPropertyTypeCustom())
                                {
                                    codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = " +
                                        $"entity.{property.Name}.ToDto(),");
                                    propertiesWithCollectionTypes.Add((property, pds));
                                }
                                else
                                    codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name},");
                            }
                            else
                            {
                                if (property.IsOfTypeClass() || property.IsOfTypeStruct())
                                {
                                    if (property.Type.NullableAnnotation == NullableAnnotation.Annotated)
                                        codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name}?.ToDto() ?? null,");
                                    else
                                        codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name}.ToDto(),");
                                }
                                else
                                    codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name},");
                            }
                        }
                    }

                    codeBuilder.AppendLine($"\t\t\t\t}};");
                    codeBuilder.AppendLine("\t\t}");
                    codeBuilder.AppendLine("\t}");

                    foreach (var propertyTuple in propertiesWithCollectionTypes)
                    {
                        var namedType = propertyTuple.Symbol.Type as INamedTypeSymbol;
                        codeBuilder.AppendLine($"\tpublic static class " +
                            $"EntityExtensions{Guid.NewGuid().ToString().Replace("-", string.Empty)}");
                        codeBuilder.AppendLine("\t{");

                        if (namedType?.IsGenericType ?? false)
                        {
                            codeBuilder.AppendLine($"\t\tpublic static " +
                                $"{namedType.BuildDtoTypeWithGenericTypeArgs(propertyTuple.Syntax, propertyTuple.Symbol)} " +
                                $"ToDto(this {namedType.Name()} entities)");

                            if (namedType.IsDictionary())
                            {
                                codeBuilder.AppendLine("\t\t{");
                                codeBuilder.AppendLine($"\t\t\t if (entities == null) " +
                                    $"return new {namedType.BuildDtoTypeWithGenericTypeArgs(propertyTuple.Syntax, propertyTuple.Symbol)}();");
                                codeBuilder.AppendLine("\t\t\t return null; //🤷‍♂️");
                                codeBuilder.AppendLine("\t\t}");
                            }
                            else
                            {
                                codeBuilder.AppendLine("\t\t{");
                                codeBuilder.AppendLine($"\t\t\t if (entities == null) " +
                                    $"return Array.Empty<" +
                                    $"{namedType.TypeArguments.First().Name}Dto>();");
                                codeBuilder.AppendLine("\t\t\t return entities.Select(x => x.ToDto()).ToList();");
                                codeBuilder.AppendLine("\t\t}");
                            }
                        }
                        else
                        {
                            codeBuilder.AppendLine($"\t\tpublic static " +
                                $"{propertyTuple.Symbol.Type.Name().Replace("[]", string.Empty)}Dto[] " +
                                $"ToDto(this {propertyTuple.Symbol.Type.Name()} entities)");
                            codeBuilder.AppendLine("\t\t{");
                            codeBuilder.AppendLine($"\t\t\t if (entities == null) " +
                                $"return Array.Empty<" +
                                $"{propertyTuple.Symbol.Type.Name().Replace("[]", string.Empty)}Dto>();");
                            codeBuilder.AppendLine("\t\t\t return entities.Select(x => x.ToDto()).ToArray();");
                            codeBuilder.AppendLine("\t\t}");
                        }

                        codeBuilder.AppendLine("\t}");
                    }

                    codeBuilder.AppendLine("}");

                    context.AddSource(generatedDtoClassName,
                        SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
                    codeBuilder.Clear();
                }
            }

            timer.Stop();
            context.Information(
                $"DTO source generation took {timer.Elapsed}");
        }        

        public void Initialize(GeneratorInitializationContext context) =>
            context.RegisterForSyntaxNotifications(() => new TargetTypeTracker());
    }

    internal static class LoggerExtensions
    {
        internal static void Warning(
            this GeneratorExecutionContext context, string logMessage) =>
            Log(context, "WARN", logMessage);

        internal static void Error(
            this GeneratorExecutionContext context, string logMessage) =>
            Log(context, "ERR", logMessage);

        internal static void Information(
            this GeneratorExecutionContext context, string logMessage) =>
            Log(context, "INFO", logMessage);

        private static void Log(
            GeneratorExecutionContext context,
            string level, string logMessage) =>
            context.AddSource($"LOGS{DateTime.UtcNow.Ticks}",
                SourceText.From($"// {level}: {DateTimeOffset.Now} {logMessage}",
                Encoding.UTF8));
    }

    internal static class CodeBuilderExtensions
    {
        internal static StringBuilder AddDefaultNamespaces(this StringBuilder codeBuilder)
        {
            return codeBuilder.AppendLine("using System;")
                .AppendLine("using System.Collections.Generic;")
                .AppendLine("using System.Linq;")
                .AppendLine("using System.Collections.Immutable;")
                .AppendLine("using System.Collections.Concurrent;");
        }

        internal static StringBuilder StartDtoNamespace(this StringBuilder codeBuilder, string dtoNamespace)
        {
            return codeBuilder.AppendLine($"using {dtoNamespace};")
                .AppendLine($"namespace {dtoNamespace}.Dtos")
                .AppendLine("{");
        }

        internal static StringBuilder StartClass(this StringBuilder codeBuilder, string className)
        {
            return codeBuilder
                .AppendLine($"\tpublic class {className}")
                .AppendLine("\t{");
        }

        internal static StringBuilder EndClass(this StringBuilder codeBuilder)
        {
            return codeBuilder.AppendLine("\t}");
        }

        internal static StringBuilder StartExtensionClass(this StringBuilder codeBuilder)
        {
            return codeBuilder
                .AppendLine($"\tpublic static class EntityExtensions{Guid.NewGuid().ToString().Replace("-", string.Empty)}")
                .AppendLine("\t{");
        }

        internal static StringBuilder StartExtensionMethod(
            this StringBuilder codeBuilder, 
            string returnTypeName, 
            string inputTypeName)
        {
            return codeBuilder
                .AppendLine($"\t\tpublic static {returnTypeName} ToDto(this {inputTypeName} entity)")
                .AppendLine("\t\t{")
                .AppendLine($"\t\t\t\treturn new {returnTypeName}")
                .AppendLine($"\t\t\t\t{{");
        }

        internal static StringBuilder AddAutoGenComment(this StringBuilder codeBuilder) =>
            codeBuilder.AppendLine("// Generated by DtoGenerators © Aman Agrawal");

        internal static StringBuilder AddDtoConvertedPropertyMapping(
            this StringBuilder codeBuilder, 
            string propertyName)
        {
            return codeBuilder.AppendLine($"\t\t\t\t\t{propertyName} = entity.{propertyName}.ToDto(),");
        }

        internal static StringBuilder AddDirectPropertyMapping(
            this StringBuilder codeBuilder, 
            string propertyName)
        {
            return codeBuilder.AppendLine($"\t\t\t\t\t{propertyName} = entity.{propertyName},");
        }
    }
}