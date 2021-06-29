using DtoGenerators;

namespace SourceGenerators.TestApp.Domain
{
    [GenerateMappedDto]
    public class Address
    {
        public Address(string streetName, int houseNumber, string postCode)
        {
            StreetName = streetName;
            HouseNumber = houseNumber;
            PostCode = postCode;
        }

        public string StreetName { get; }
        public int HouseNumber { get; }
        public string PostCode { get; }
    }
}