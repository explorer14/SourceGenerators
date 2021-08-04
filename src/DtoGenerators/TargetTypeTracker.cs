using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

#nullable enable
namespace DtoGenerators
{
    internal class TargetTypeTracker : ISyntaxContextReceiver
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
}
#nullable restore