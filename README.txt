# MirrorpunchSteam

A [Mirror](https://github.com/vis2k/Mirror) transport using [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)

## Getting Started
* Make sure you have both Mirror and Facepunch.Steamworks in your project
* Download and place MirrorpunchSteam in your Assets folder or an Assets subdirectory as desired
* Add the MirrorpunchSteam transport component to a GameObject along with the Mirror NetworkManager
* Assign MirrorpunchSteam to the Transport field on the NetworkManager
	* You can also use MirrorpunchSteam along with another transport (e.g. Telepathy) by using a MultiplexTransport and assigning the transports accordingly

## Settings
* **Maintain Client** [bool] - Enable Maintain Client if you do not have another script initializing or maintaining the Steamworks client; MirrorpunchSteam will take care of initialization and cleanup
* **App Id** [uint] - You must specify the app id here if Maintain Client is enabled in order for Steamworks to be initialized
* **Max Connections** [uint] - The max number of connections that can be accepted if acting as a host
* **Tick Rate** [double, ms] - The tick rate of the receive loop in milliseconds; common tickrates are provided in MirrorpunchCommon as constant values
* **Allow Relay** [bool] - If enabled, P2P connects can fall back on Steam server relay if direct connection or NAT traversal can't be established
* **Enable Transport** [bool] - Allows you to enable or disable the transport as desired, should generally be enabled; if disabled, Available() will return false
* **Force Online** [bool] - If enabled, Available() will return false if SteamClient.IsLoggedOn is false; not advisable in most conditions, and especially for games which you want to run in offline mode

## Building
When you build your game, ensure that you place a text file titled **steam_appid.txt** into the game's root directory containing your game's unique app id.

## Play
When running the game standalone or in editor, it should show you as playing your game. If not, check that the app id is correct and Steam is running on your computer.

