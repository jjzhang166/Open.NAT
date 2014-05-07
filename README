Open.Nat
======

Open.Nat is a lightweight and easy-to-use class library to allow port forwarding in NAT devices (Network Address Translator) that support Universal Plug & Play (UPNP) and/or Port Mapping Protocol (PMP). 

NATed computers cannot be reached from outside and this is particularly painful for peer-to-peer or friend-to-friend software. Open.Nat helps developers to achieve peer-to-peer communication providing the nated computers' external IP address and port.   
to develop peer-to-peer applications in .NET and Mono. 


Goals
-----
The main goal is to simplify communication amoung computers behind NAT devices that support UPNP and/or PMP providing a clean and easy interface to get the external IP address and map ports. 


Example
--------


```c#
NatUtility.DeviceFound += (sender, args) => {
  Console.WriteLine("It got it!!");
  var ip = await device.GetExternalIPAsync();
  Console.WriteLine("The external IP Address is: " + ip);
};

NatUtility.Initialize();
NatUtility.StartDiscovery();
```


Development
-----------
Open.Nat is been developed by [Lucas Ontivero](http://geeks.ms/blogs/lontivero) ([@lontivero](http://twitter.com/lontivero)). You are welcome to contribute code. You can send code both as a patch or a GitHub pull request.

Note that Open.Nat is still very much work in progress. 
