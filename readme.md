Including in Unity project:

*  copy DisguiseUnityRenderStream plugin to the Unity assets folder.
*  If you get an error RegistryKey/Registry is not part of namespace Microsoft.Win32 go to File > Build Settings > Player Settings... > Other Settings:
    *  If available change Api Compatibility level to .NET 4.x.
    *  Else change Scripting runtime version to .NET 3.5 equivalent.
    *  If there is an error about unsafe code tick allow 'unsafe' code, this is project wide and should not be done unless unity ignores the asmdef for this plugin.
 
Using the plugin:

* Optionally: Attach a Disguise RenderStream component to the appropriate game object for remote parameter control or Playable Director time control
* Build a Windows/x86_64 executable and copy it to your RenderStream Projects folder
* Launch executable through d3's RenderStream layer

Notes:

*  Without a disguise instance running and sending data to the unity camera if you run your scene the camera will be placed at the world origin and cannot be moved (it's position is overriden every frame).
*  Firewall settings can interfere with NDI, if there is a problem use the NDI Studio Monitor to ensure that you can receive the stream. If you aren't receiving disable the firewall on both machines.
*  If you still can't ensure the above step has been performed.
*  If the firewall is the problem check your outbound firewall rules on the unity machine, and the inbound firewall rules on the disguise machine for either software being specifically blocked.