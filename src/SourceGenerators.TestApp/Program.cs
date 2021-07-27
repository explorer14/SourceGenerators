using System;
using Newtonsoft.Json;
using SourceGenerators.TestApp.Domain;
using SourceGenerators.TestApp.Domain.Dtos;

namespace SourceGenerators.TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var e = new Employee(
                "asasd", DateTime.Now, DateTime.Now, 
                new Address("asdasd",2,"asdasd"));
            e.AllocateAsset(new CompanyAsset("ASB", 100, "!@!@#"));
            e.AllocateAsset(new CompanyAsset("DFGHJ", 200, "!@!@#"));
            e.AllocateAsset(new CompanyAsset("@$%Y", 300, "!@!@#"));
            e.AllocateAsset(new CompanyAsset("sdfasas", 300, "!asd#"));

            var dto = e.ToDto();
            Console.WriteLine(JsonConvert.SerializeObject(dto));
        }
    }
}
