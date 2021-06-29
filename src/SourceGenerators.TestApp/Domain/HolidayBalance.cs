using DtoGenerators;

namespace SourceGenerators.TestApp.Domain
{
    [GenerateMappedDto]
    public struct HolidayBalance
    {
        public HolidayBalance(decimal balance) =>
            Balance = decimal.Round(balance, 2);

        public decimal Balance { get; }
    }
}