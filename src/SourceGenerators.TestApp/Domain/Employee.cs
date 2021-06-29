using DtoGenerators;
using System;
using System.Collections.Generic;

namespace SourceGenerators.TestApp.Domain
{
    [GenerateMappedDto]
    public sealed class Employee
    {
        private readonly List<CompanyAsset> assetsAllocated = new List<CompanyAsset>();

        public Employee(
            string name,
            DateTime dateOfBirth,
            DateTime dateOfJoining,
            Address address)
        {
            Id = Guid.NewGuid();
            Name = name;
            DateOfBirth = dateOfBirth;
            DateOfJoining = dateOfJoining;
            Address = address;
            HolidayBalance = new HolidayBalance(25);
            PromotionCodes.Add(100, "TEST");
            PromotionCodes.Add(200, "TEST2");
        }

        public Guid Id { get; }
        public string Name { get; }
        public DateTime DateOfBirth { get; }
        public DateTime DateOfJoining { get; }
        public IReadOnlyCollection<CompanyAsset> AssetsAllocated => assetsAllocated;

        public Address Address { get; }

        public HolidayBalance HolidayBalance { get; }

        public Dictionary<int, string> PromotionCodes { get; } = new Dictionary<int, string>();

        public void AllocateAsset(CompanyAsset asset)
        {
            assetsAllocated.Add(asset);
        }
    }
}