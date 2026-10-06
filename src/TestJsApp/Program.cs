using System;
using System.IO;
using Jint;

public class Stub {
    public void test(object obj) {
        var arr = obj as object[];
        Console.WriteLine($"Is object[]? {arr != null}. Type is: {obj.GetType().Name}");
    }
}
class Program {
    static void Main() {
        var engine = new Engine();
        engine.SetValue("stub", new Stub());
        engine.Execute("stub.test([1,2]); var x = []; x[0]=1; stub.test(x);");
    }
}
