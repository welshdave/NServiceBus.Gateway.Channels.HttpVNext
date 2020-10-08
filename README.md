NServiceBus.Gateway.Channels.HttpVNext
===================
An HTTP channel implementation for NServiceBus.Gateway that doesn't use HTTP Headers for message content or metadata. As such it should be easier to use this channel where headers may be modified (e.g. when a gateway is behind a reverse proxy such as NGINX).

## Usage

To use this channel, install the NuGet package and configure the gateway to use it when receiving messages:

```
var gatewayConfig = endpointConfiguration.Gateway();
gatewayConfig.ChannelFactories(s => new HttpVNextChannelSender(), r => new HttpVNextChannelReceiver());

gatewayConfig.AddReceiveChannel("http://Headquarter.mycorp.com/", "httpVNext");
```

The final step is to configure the gateway to use the new channel when transmitting messages:

```
var gatewayConfig = endpointConfiguration.Gateway();
gatewayConfig.ChannelFactories(s => new HttpVNextChannelSender(), r => new HttpVNextChannelReceiver());

gatewayConfig.AddSite("SiteA", "http://SiteA.mycorp.com");

```

## Licenses

### [SimpleJson](https://github.com/facebook-csharp-sdk/simple-json/) 

SimpleJson is licensed under the MIT license as described [here](https://github.com/facebook-csharp-sdk/simple-json/blob/master/LICENSE.txt).

SimpleJson sources are compiled into NServiceBus.Gateway.Channels.HttpVNext as allowed under the license terms found [here](https://github.com/facebook-csharp-sdk/simple-json/blob/master/LICENSE.txt).
