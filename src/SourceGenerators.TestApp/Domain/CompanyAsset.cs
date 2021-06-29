using DtoGenerators;

namespace SourceGenerators.TestApp.Domain
{
    [GenerateMappedDto]
    public class CompanyAsset
    {
        public CompanyAsset(string name, decimal worth, string code)
        {
            Name = name;
            Worth = worth;
            AssetCode = new AssetCode(code);
        }

        public string Name { get; }
        public decimal Worth { get; }
        public AssetCode AssetCode { get; }
    }
}