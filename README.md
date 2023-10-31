> TL;DR - Increase your game’s performance without any noticeable loss of quality.
  
In the endless pursuit of higher framerates, DLSS/FSR2 can often do miracles by using deep learning, AI upscaling in real-time to render a game at a lower resolution and “magically” upscale its resolution without any noticeable loss in quality.

*The Perf Mod for V-Rising Mod is the first time DLSS/FSR2 has ever been modded into a game without any existing next-gen upscaler support (i.e., existing FSR 2.0 support, etc.).*

Unlike the game’s existing FSR 1.0 support, in addition to using deep learning to upscale, DLSS/FSR2 uses real-time knowledge of the game such its depth buffer, motion vectors, etc. so that upscaling results are as reliable and accurate as possible.

Now comes with FSR2 support! Non-RTX card owners can now also benefit from FSR2.

New in v1.1.0: 
- I implemented XeSS as well, for those who want to try it, it has less ghosting but blurrier output.
- Added DLAA for CPU bound player, you can make use of the extra GPU power to use the best AA option.

## How Well Does DLSS/FSR2/XeSS Work in V Rising?

Your results will vary depending on whether your CPU or GPU is your game’s bottleneck. Assuming that the GPU is the bottleneck, this mod can hugely increase your performance without any noticeable loss in quality. 
This is especially useful if you’re pushing the game to run on higher res monitors or just want to absolutely crank all the graphics to the max.

Accroding to Slafs with an 6700XT, he's got following results at 4K resolution

> - Native: 46 FPS
> - Quality: 70 FPS
> - Balanced: 82 FPS
> - Performance: 101 FPS
> - Ultra Performance: 115 FPS

## Preview
Click on it to see side by side comparison

[![Comparison](https://imgsli.com/i/5ecf924b-f676-4515-83d8-5dcfc01a5efd.jpg)](https://imgsli.com/MTMxMzE4/)


## Requirements

DLSS requires an Nvidia RTX card. 

FSR2 now added, and it can work with most cards.

## Building
Requires at least .NET Core 6.0 to build.

From the command line:
```
dotnet build PerfMod.sln
```

## How To Use

After installing to the BepinEx plugins folder just like any other mod, launch the game and load your save 

- `CTRL + NumPad 1/2/3` to choose DLSS/FSR2/XeSS, press again to turn it off.
- `CTRL + ↑/↓` to cycle through upscaling profiles(from Performance to Quality). 
- `CTRL + ←/→` to adjust the sharpness value (`CTRL + Keypad0` resets sharpening to default settings).

All of above settings can be tweaked in the config file.

### Important Notes

 - **Keep AA (especially TAA) *disabled* when DLSS/FSR2/XeSS is enabled**.
 - If you run into any problem, feel free to open an issue at [VRisingPerfMod
](https://github.com/PureDark/VRisingPerfMod/issues "https://github.com/PureDark/VRisingPerfMod/issues")

## Credits
 - PureDark/暗暗十分 - Author
 - elliotttate - Testing and writing readme
 - Slafs - Testing and helping with fixing a bug related to RDNA2

 *IMPORTANT: If anyone wants to repost this somewhere else, be sure to mention my name, not just PureDark, but also my Chinese ID, just copy paste it.*


## Support

Consider support me on patron and motivate me to work on more games!

Any support will be appreciated and you might even commision what game you want DLSS to be modded in.

[My Patron Page](https://www.patreon.com/PureDark)


## Changelog

`v1.1.0`
 - Implemented XeSS and DLAA
 - Changed Hotkeys

`v1.0.1`
 - Fixed crashing when switching to DLSS
