using DtoGenerators;

namespace SourceGenerators.TestApp.Domain
{
    [GenerateMappedDto]
    public struct AssetCode
    {
        public AssetCode(string code)
        {
            Code = code;
        }

        public string Code { get; }
    }
}