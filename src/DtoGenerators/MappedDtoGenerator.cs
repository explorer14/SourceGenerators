using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

#nullable enable
namespace DtoGenerators
{
    [Generator]
    public class MappedDtoGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var timer = Stopwatch.StartNew();
            var targetTypeTracker = context.SyntaxContextReceiver as TargetTypeTracker;
            var codeBuilder = new StringBuilder();

            foreach (var typeNode in targetTypeTracker.TypesNeedingDtoGening)
            {
                var typeNodeSymbol = context.Compilation
                    .GetSemanticModel(typeNode.SyntaxTree)
                    .GetDeclaredSymbol(typeNode);

                var entityClassNamespace = typeNodeSymbol.ContainingNamespace?.ToDisplayString()
                    ?? "NoNamespace";

                var generatedDtoClassName = $"{typeNodeSymbol.Name}Dto";

                codeBuilder.AppendLine("using System;");
                codeBuilder.AppendLine("using System.Collections.Generic;");
                codeBuilder.AppendLine("using System.Linq;");
                codeBuilder.AppendLine("using System.Collections.Immutable;");
                codeBuilder.AppendLine("using System.Collections.Concurrent;");
                codeBuilder.AppendLine($"using {entityClassNamespace};");

                codeBuilder.AppendLine($"namespace {entityClassNamespace}.Dtos");
                codeBuilder.AppendLine("{");

                codeBuilder.AppendLine($"\tpublic class {generatedDtoClassName}");
                codeBuilder.AppendLine("\t{");

                var allProperties = typeNode.Members.OfType<PropertyDeclarationSyntax>();

                foreach (var property in allProperties)
                    codeBuilder.AppendLine($"\t\t{property.BuildDtoProperty(context.Compilation)}");

                codeBuilder.AppendLine("\t}");

                codeBuilder.AppendLine($"\tpublic static class EntityExtensions{Guid.NewGuid().ToString().Replace("-", string.Empty)}");
                codeBuilder.AppendLine("\t{");
                codeBuilder.AppendLine($"\t\tpublic static {generatedDtoClassName} ToDto(this {typeNode.Identifier.ValueText} entity)");
                codeBuilder.AppendLine("\t\t{");
                codeBuilder.AppendLine($"\t\t\t\treturn new {generatedDtoClassName}");
                codeBuilder.AppendLine($"\t\t\t\t{{");

                var propertiesWithCollectionTypes =
                    new List<(IPropertySymbol Symbol, PropertyDeclarationSyntax Syntax)>();

                foreach (var pds in allProperties)
                {
                    var symbol = context.Compilation
                             .GetSemanticModel(pds.SyntaxTree)
                             .GetDeclaredSymbol(pds);
                    var property = (symbol as IPropertySymbol);
                    var propertyType = property.Type as INamedTypeSymbol;

                    if (propertyType?.IsGenericType ?? false)
                    {
                        if (property.IsAtleastOneTypeArgumentCustomType())
                        {
                            codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name}.ToDto(),");
                            propertiesWithCollectionTypes.Add((property, pds));
                        }
                        else
                            codeBuilder.AppendLine($"\t\t\t\t\t{property.Name} = entity.{property.Name},");
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

            timer.Stop();
            context.AddSource("PERF",
                SourceText.From($"// {DateTimeOffset.Now} DTO source generation took {timer.Elapsed}",
                Encoding.UTF8));
        }

        private static void ReportWarningIfReceiverNotFound(
            GeneratorExecutionContext context,
            TargetTypeTracker targetTypeTracker)
        {
            if (targetTypeTracker == null)
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DTOGEN",
                            "DtoGenerator",
                            "No ISyntaxContextReciever implementation found!",
                            "DTO Gen",
                            DiagnosticSeverity.Warning, true),
                        Location.None));
        }

        private void AddAutoGenComment(StringBuilder codeBuilder) =>
            codeBuilder.AppendLine("// Generated by DtoGenerators © Aman Agrawal");        

        public void Initialize(GeneratorInitializationContext context) =>
            context.RegisterForSyntaxNotifications(() => new TargetTypeTracker());
    }

    public class TargetTypeTracker : ISyntaxContextReceiver
    {
        public IImmutableList<TypeDeclarationSyntax> TypesNeedingDtoGening =
            ImmutableList.Create<TypeDeclarationSyntax>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is TypeDeclarationSyntax cdecl)
                if (cdecl.IsDecoratedWithAttribute("generatemappeddto"))
                    TypesNeedingDtoGening = TypesNeedingDtoGening.Add(
                        context.Node as TypeDeclarationSyntax);
        }
    }

    internal static class SourceGenExtns
    {
        internal static bool IsDecoratedWithAttribute(
            this TypeDeclarationSyntax cdecl, string attributeName) =>
            cdecl.AttributeLists
                .SelectMany(x => x.Attributes)
                .Any(x => x.Name.ToString().ToLower() == attributeName);

        internal static string BuildDtoProperty(
            this PropertyDeclarationSyntax pds,
            Compilation compilation)
        {
            var symbol = compilation
                .GetSemanticModel(pds.SyntaxTree)
                .GetDeclaredSymbol(pds);

            var property = (symbol as IPropertySymbol);
            var propertyType = property.Type as INamedTypeSymbol;

            if (propertyType?.IsGenericType ?? false)
                return $"public {propertyType.BuildDtoTypeWithGenericTypeArgs(pds, property)} {property.Name} {{get; set;}}";

            if (property.Type.TypeKind == TypeKind.Array)
            {
                var elementTypeSymbol = property.Type as IArrayTypeSymbol;

                if ((elementTypeSymbol.ElementType.IsClass() ||
                    elementTypeSymbol.ElementType.IsStruct()) &&
                    property.IsPropertyTypeCustom())
                    return $"public {elementTypeSymbol.ElementType.Name()}Dto[] {property.Name} {{get; set;}}";
                else
                    return $"public {property.Type.Name()} {property.Name} {{get; set;}}";
            }

            if (property.IsOfTypeClass() || property.IsOfTypeStruct())
            {
                if (property.Type.NullableAnnotation == NullableAnnotation.Annotated)
                    return $"public {property.Type.Name}Dto? {property.Name} {{get; set;}}";

                return $"public {property.Type.Name()}Dto {property.Name} {{get; set;}}";
            }
            else
            {
                if (property.Type.NullableAnnotation == NullableAnnotation.Annotated)
                    return $"public {property.Type.Name()}? {property.Name} {{get; set;}}";

                return $"public {property.Type.Name()} {property.Name} {{get; set;}}";
            }
        }

        internal static string BuildDtoTypeWithGenericTypeArgs(
            this INamedTypeSymbol namedType,
            PropertyDeclarationSyntax pds,
            IPropertySymbol property)
        {
            var gns = pds.DescendantNodes()
                .OfType<GenericNameSyntax>();
            var typeArgNodes = gns.First().TypeArgumentList;

            var dtoTypeNameList = new List<string>();

            foreach (var node in typeArgNodes.Arguments)
            {
                var typeName = BuildTypeName(node, property);

                if (typeName!=null)
                    dtoTypeNameList.Add($"{typeName}");
            }

            return @$"{namedType.Name}<{string.Join(",", dtoTypeNameList)}>";
        }

        private static string? BuildTypeName(TypeSyntax node, IPropertySymbol property)
        {
            if (node is IdentifierNameSyntax ins)
            {
                var namedType = property.Type as INamedTypeSymbol;
                var typeArg = namedType!.TypeArguments
                    .First(x=>x.Name == ins.Identifier.ValueText);

                if (property.IsPropertyTypeCustom(typeArg))
                    return $"{ins.Identifier.ValueText}Dto";

                return $"{ins.Identifier.ValueText}";
            }
                
            if (node is PredefinedTypeSyntax pts)
                return $"{pts.Keyword.ValueText}";

            return default;
        }

        internal static bool IsOfTypeClass(this IPropertySymbol propSym) =>
            propSym.Type.IsClass() &&
            propSym.IsPropertyTypeCustom();

        internal static bool IsOfTypeStruct(this IPropertySymbol propSym) =>
            propSym.Type.IsStruct() &&
            propSym.IsPropertyTypeCustom();

        internal static bool IsPropertyTypeCustom(this IPropertySymbol property) =>
            property.IsPropertyTypeCustom(property.Type);

        internal static bool IsPropertyTypeCustom(this IPropertySymbol property, 
            ITypeSymbol type) => 
            type.ToDisplayString().StartsWith(
                property.ContainingNamespace.ToDisplayString());

        internal static bool IsClass(this ITypeSymbol namedType) =>
            namedType.IsReferenceType && namedType.TypeKind == TypeKind.Class;

        internal static bool IsStruct(this ITypeSymbol namedType) =>
            namedType.IsValueType && namedType.TypeKind == TypeKind.Struct;

        internal static string Name(this ITypeSymbol typeSymbol) =>
            typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        internal static bool IsDictionary(this INamedTypeSymbol namedTypeSymbol) =>
            namedTypeSymbol.ConstructedFrom.Name.Contains("Dictionary");

        internal static bool IsAtleastOneTypeArgumentCustomType(
            this IPropertySymbol property)
        {
            var typeArgs = (property.Type as INamedTypeSymbol)!.TypeArguments;

            foreach (var type in typeArgs)
            {
                var isCustom = property.IsPropertyTypeCustom(type);

                if (isCustom)
                    return true;
            }

            return false;
        }
    }
}
#nullable restore