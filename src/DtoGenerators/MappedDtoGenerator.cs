using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace DtoGenerators
{
    [Generator]
    public class MappedDtoGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var targetTypeTracker = context.SyntaxContextReceiver as TargetTypeTracker;
            ReportWarningIfReceiverNotFound(context, targetTypeTracker);

            foreach (var typeNode in targetTypeTracker.TypesNeedingDtoGening)
            {
                context.AddSource("TypesFound", 
                    SourceText.From($"// Found {typeNode.Identifier.ValueText}"));
            }
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

        public void Initialize(GeneratorInitializationContext context) => 
            context.RegisterForSyntaxNotifications(() => new TargetTypeTracker());
    }

    internal class TargetTypeTracker : ISyntaxContextReceiver
    {
        public IImmutableList<TypeDeclarationSyntax> TypesNeedingDtoGening =
            ImmutableList.Create<TypeDeclarationSyntax>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node);

            if ((symbol as ITypeSymbol).GetAttributes()
                .Count(x=>$"{x.AttributeClass.Name}Attribute" == nameof(GenerateMappedDtoAttribute)) > 0)
                TypesNeedingDtoGening = TypesNeedingDtoGening.Add(context.Node as TypeDeclarationSyntax);
        }
    }
}