using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace DtoGenerators
{
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