using Dekaf.ShareConsumer;

namespace Dekaf.Internal;

/// <summary>Provides share consumer configuration without constructing a network client.</summary>
internal interface IShareConsumerConfigurationRegistration
{
    ShareConsumerOptions GetOptions(IServiceProvider provider);
}
