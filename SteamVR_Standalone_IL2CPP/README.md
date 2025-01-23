# SteamVR_Standalone_IL2CPP
A modified SteamVR plugin that can be injected into Non-VR enabled Unity projects for VR rendering and VR input/interaction. 

Fork from [DSprtn/SteamVR_Standalone_IL2CPP](https://github.com/DSprtn/SteamVR_Standalone_IL2CPP) 

Modified for IL2CPP. Specifically tailored for Gunfire Reborn. 

#### INITIALIZATION

Call SteamVR.Initialize(false) before the game loads. Add SteamVR_Camera on the main player character. 
You may need to tweak the Expand() function in SteamVR_Camera depending on the needs of your game.
For rendering UI look at Eusth's VRGIN approach or at GTFO VR if you're ok with just using SteamVR_Overlay for it.
