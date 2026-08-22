using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class ActivityStateAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<ActivityStateAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task InstancePropertyIncrement_Reports()
        => Verify(Stubs + """
            public class Act
            {
                private int Attempts { get; set; }

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    {|TMP3203:Attempts += 1|};
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task InstanceFieldAssignment_Reports()
        => Verify(Stubs + """
            public class Act
            {
                private int _count;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    {|TMP3203:_count = 5|};
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task ThisFieldAssignment_Reports()
        => Verify(Stubs + """
            public class Act
            {
                private int _count;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    {|TMP3203:this._count = 5|};
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task IncrementInLambda_Reports()
        => Verify(Stubs + """
            public class Act
            {
                private int _count;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    System.Action a = () => { {|TMP3203:_count++|}; };
                    a();
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task StaticFieldAssignment_DoesNotReport()
        => Verify(Stubs + """
            public class Act
            {
                private static int _count;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    _count = 5;
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task LocalVariableAssignment_DoesNotReport()
        => Verify(Stubs + """
            public class Act
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do()
                {
                    var x = 0;
                    x = 5;
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task OtherObjectFieldAssignment_DoesNotReport()
        => Verify(Stubs + """
            public class Act
            {
                private int _count;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Do(Act other)
                {
                    other._count = 5;
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task FieldAssignmentOutsideActivity_DoesNotReport()
        => Verify(Stubs + """
            public class Act
            {
                private int _count;

                public void NotAnActivity()
                {
                    _count = 5;
                }
            }
            """);
}
