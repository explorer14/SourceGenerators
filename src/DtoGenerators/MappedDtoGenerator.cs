using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace DtoGenerators
{
    [Generator]
    public class MappedDtoGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var targetTypeTracker = context.SyntaxContextReceiver as TargetTypeTracker;
            ReportWarningIfReceiverNotFound(context, targetTypeTracker);
            var codeBuilder = new StringBuilder();

            foreach (var typeNode in targetTypeTracker.TypesNeedingDtoGening)
                codeBuilder.AppendLine($"// Found {typeNode.Identifier.ValueText}");

            context.AddSource("TypesFound", 
                SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
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
    }
}