using System;
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

            var dto = e.ToDto();
        }
    }
}
