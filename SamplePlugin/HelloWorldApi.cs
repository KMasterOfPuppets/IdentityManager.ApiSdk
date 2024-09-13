using QBM.CompositionApi.Definition;

namespace Api
{
    public class HelloWorldApi : IApiProviderFor<SampleApiProject>
    {
        public void Build(IApiBuilder builder)
        {
            builder.AddMethod(Method.Define("helloworld")
                .HandleGet(request => "Hello world!")
            );
        }
    }
}
