using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;

namespace testbson
{
    class Program
    {
        //为了简化通讯协议，TCP用bson，http用JSON
        //Newtonsoft.Json 和  Newtonsoft.Json.Bson
        static void Main(string[] args)
        {
            Newtonsoft.Json.Linq.JValue v = new Newtonsoft.Json.Linq.JValue(11.0);
            Newtonsoft.Json.Linq.JObject obj = new Newtonsoft.Json.Linq.JObject();
            obj["abc"] = v;
            var stream = new System.IO.MemoryStream();
            Newtonsoft.Json.Bson.BsonDataWriter w = new Newtonsoft.Json.Bson.BsonDataWriter(stream);
            obj.WriteTo(w);
            var bts = stream.ToArray();

            Newtonsoft.Json.Bson.BsonDataReader r = new Newtonsoft.Json.Bson.BsonDataReader(new System.IO.MemoryStream(bts));
            var token = Newtonsoft.Json.Linq.JToken.ReadFrom(r);
            Console.WriteLine("Hello World!");

            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class A{public  int aaa(){return 3;}}");

            var op = new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary);
            var ref1 = MetadataReference.CreateFromFile("needlib\\mscorlib.dll");
            var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("aaa.dll", new[] { tree },
               new[] { ref1 }, op);
            var result = comp.Emit("e:\\111.dll", pdbPath: "e:\\111.pdb");
        }
    }
}
