

using JsonContextDb.TestApp;

//await Tester.Main();

Console.WriteLine("---------------------------------------------");
await EFTester.Main();
Console.WriteLine("---------------------------------------------");

Console.WriteLine("---------------------------------------------");
await JCTester.Main();
Console.WriteLine("---------------------------------------------");

