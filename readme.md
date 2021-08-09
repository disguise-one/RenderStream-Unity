# RenderStream Unity Plugin

![alt text](https://download.disguise.one/media/6921/unity.png)

For more info please refer to our [RenderStream and Unity Help page](https://help.disguise.one/Content/Configuring/Render-engines/RenderStream-Unity.htm)

A **Demo Unity Project** can be found on the [disguise Resources page](https://download.disguise.one/#resources)

## Importing the RenderStream Unity Plugin

*  Copy/Import the top-level **DisguiseUnityRenderStream** folder to your Unity Project's **Assets** folder.
*  If you get an error RegistryKey/Registry is not part of namespace Microsoft.Win32 go to File > Build Settings > Player Settings... > Other Settings:
    *  If available **change Api Compatibility level to .NET 4.x.**
    *  Else, change Scripting runtime version to .NET 3.5 equivalent.
    *  If there is an error about unsafe code tick allow 'unsafe' code, this is project wide and should not be done unless unity ignores the asmdef for this plugin.
 
## Using the RenderStream Unity Plugin

The act of importing the **DisguiseUnityRenderStream** plugin to your Unity Project's Asset folder, is enough to enable RenderStream in your built executable.

More control can be added using the included Disguise RenderStream components:

* **To enable control of GameObject(s) properties**, attach a Disguise RenderStream > **Remote Parameters** component to the appropriate game object for remote parameter control.
   * Note: you can enable/disable the exact GameObject properties using the List editor in the Unity Inspector.
* **To add designer timeline control**, attach a Disguise RenderStream > **Time Control** component to a Playable Director

## Building a RenderStream asset for disguise designer

To use your Unity Project with disguise designer, you build an executable and import it into your designer project. To do this:
* Ensure Build Settings are set to build a **Windows x86_64** executable
* Copy the build folder to your **RenderStream Projects** folder
* In designer, set up a new RenderStream Layer and point the Asset to your built executable.

## Notes:

*  Without a disguise instance running and sending data to the Unity camera, if you run your scene the camera will be placed at the world origin and cannot be moved (it's position is overriden every frame).
*  Firewall settings can interfere with transport streams. You may see a Windows security dialog pop-up the first time you run the asset.
   * Click Enable / Allow on this Windows pop-up to allow RenderStream communication to/from designer.
*  If the firewall is the problem check your outbound firewall rules on the Unity machine, and the inbound firewall rules on the disguise machine for either software being specifically blocked.
