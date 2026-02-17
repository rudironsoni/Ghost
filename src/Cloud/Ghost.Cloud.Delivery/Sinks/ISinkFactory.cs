using Ghost.Cloud.Contracts.Delivery;

namespace Ghost.Cloud.Delivery.Sinks;

public interface ISinkFactory
{
    public IResultSink Create(ResultSink config);
}
