<div align="center">
  <img src="Textures/Titlebar.png" />
  <a href="https://discord.gg/SyZcuTPXZA">
    <img src="Textures/Discord%20Button.png" alt="Discord Link"/>
  </a>
</div>

This project was previously owned by rygo6. It has been passed on for me to own and work on.
This is the official repository for this project.

This is free. Actually read all of this.

# Kanna Protecc

This is a rather invasive anti-avatar-ripping system to be used for VRChat. It will protect against your avatar being ripped, extracted and edited. It will also protect against your avatar being ripped and re-uploaded without edits.

This system will randomize all the vertices of your avatar's mesh, then write that to disk. Then rely on a custom shader with a 32 bit key to un-randomize the vertex positions in game. This is <b>not</b> done through blend shapes. Rather this will copy, and destructively edit, the 'Basis' layer of your mesh. It will also obfuscate pretty much everything else on your avatar to confusing as a extra middle finger for the ripper.

1. [Caveats of this System](#caveats-of-this-system)

2. [Usage Instructions](#usage-instructions)

3. [How secure is this?](#how-secure-is-this)

5. [Support](#supported-shaders)

6. [Support](#support)

## Caveats of this System

1. For a user to see your avatar properly in VRChat, they must have your avatar fully shown. Shaders, animations and all. So this system is only ideal for avatars you use in worlds where most people are your friends.

2. This synchronizes the key with Avatar 3.0 parameters and does take up 32 bits. So this system can only work with avatars that use the VRChat Avatar 3.0 SDK.

3. Support for a specific shader must be manually added in a update. Currently only Poiyomi is supported, version 7.3 and newer. To request another shader to be supported, feel free to ask in the discussions tab here, or in the discord, seen in the [Support](#support) section here.

## Usage Instructions

### Backup your project before running these operations in case it doesn't work properly and causes difficult to fix, or impossible to fix, changes in your project.

#### Really do it. Close Unity, and make a full clean copy of your entire Unity Project folder. A small percentage of avatars did have odd things in their mesh that just wouldn't work, or could cause errors, and the script could leave some assets in the project in a rather messed up state.

#### Install Kanna Protecc and Poiyomi (Or another supported shader)

1. Ensure you are using latest VRC SDK.
2. Download the Poiyomi 8/8.1 package from https://github.com/poiyomi/PoiyomiToonShader or Poiyomi's patreon discord if you bought, and import it into your Unity project.
3. Click on Code -> download zip on this repo. Once downloaded, extract it. Once you have the folder, put that into your assets folder of your unity project.

#### Setup VRC Components.

1. Add the `KannaProteccRoot` component onto the root GameObject of your avatar, next to the `VRCAvatarDescriptor` component.

![Steps 1](Textures/DocSteps1.png)

2. Ensure your `VRCAvatarDescriptor` has an AnimatorController specified in the 'FX Playable Layer' slot. <b>The AnimatorController you specify should not be shared between multiple avatars, Kanna Protecc is going to write states into the controller which will need to be different for different avatars.</b>
3. Ensure there is also an `Animator` component on this root GameObject, and that its 'Controller' slot points to the same AnimatorController in the 'FX Playable Layer' slot on the `VRCAvatarDescriptor`.

![Steps 1](Textures/DocSteps2to3.png)

5. In the 'Parameters' slot of your `VRCAvatarDescriptor` ensure you have an 'Expression Parameters' object.

![Step 4](Textures/DocSteps4.png)

#### Delete your old Un-Encrypted Avatar from VRC Backend!

VRC API stores old uploads of your avatar! So if you start uploading an encrypted avatar with an ID that you previously uploaded non-encrypted, it may entirely negate any benefit this provides as rippers can just download an older version that was not encrypted.

1. Go into the VRChat SDK Inspector in the Unity Editor, then under 'Content Manager' find the avatar you wish to protect and delete it entirely from the VRC backend.
2. Go to your current avatar's `Pipeline Manager` component and click the `Detach (Optional)` button so it will generate a new avatar id on upload.

#### Encrypting and Uploading

1. Ensure any meshes you wish to have encrypted are using a compatible shader, such as Poiyomi.
2. On the `KannaProteccRoot` component click the 'Encrypt Avatar' button. This will lock all of your materials, make all necessary edits to your AnimatorController, and make a duplicate of your avatar which is encrypted. Be aware your duplicated avatar with "_Encrypted" appended to it's name will appear completely garbled in the editor. The garbled materials will be invisible to those who have you not fully shown.
3. Go to the VRChat SDK Menu then 'Build and Publish' your avatar which has '_Encrypted' appended to the name.

*I found some Poi 8/8.1 materials get into a weird state with Lock/Unlock and Kanna Protecc can't lock them. If you get errors that say something like 'Trying to Inject not-locked shader?!' go to the Poi 8/8.1 material it is complaining about and manually click the Lock/Unlock button to get it out of its weird state.*

#### Writing Keys

1. After upload completes, go to the GameObject of your encrypted avatar. Find the `Pipeline Manager` component and copy it's blueprint ID. Then paste the blueprint ID into the `Pipeline Manager` on the un-encrypted avatar and click 'Attach'. This is important.
2. Now on the KannaProteccRoot component click the 'Write Keys' button. Ensure VRC is closed when you do this, as VRC might disallow writing to the file. This will actually read in and alter the saved 3.0 parameters from your VRChat folder to include the new key so you don't have to enter them in-game. <i>This also means if you "Reset Avatar" in game through the 3.0 menu, it will reset your keys and you will need to re-export them with the 'Write Keys' button!</i>
3. This should provide ample error dialogues or console errors, so ensure no errors came up!. It should popup a success dialogue if it did it correctly. If there were issues make sure the 'Vrc Saved Params Path' actually points to your LocalAvatarData folder.
4. You only need to run 'Write Keys' once on first setup, or when you change keys.

*Ensure VRChat is closed when you write keys otherwise VRChat may just overwrite them immediately with zeroes!

#### Un-Encrypting Your Avatar

If you wish to see your avatar again as normal and not encrypted, click on your original non-encrypted avatar and click unlock materials.

## How secure is this?

I will keep transparent here without guiding rippers on how to attack your works. This is not foolproof, but the best you can get at this time. Rip wise, this cannot be currently ripped without a insane amount of work, as ripping compiled shadercode, reversing it back to unity compatible code and also getting hold of the keys would be hell. Hotswap wise, a dedicated enough ripper with experience with mods could hotswap your avatar. This is not immune to that. (Soon to be countered with trap params making even that extremely harder to do) You can however put a watermark on your avatar to drive hotswappers into being banned which they wont be able to remove, as the meshes will be encrypted, regardless of hotswap. A hotswap done with the high amount of work i have mentioned here would only get the avatar working normally in game; not in unity.

## Supported Shaders

| Supported Shader Name  | Download |
| ------------- | ------------- |
| Poiyomi | https://github.com/poiyomi/PoiyomiToonShader |

## Support

If you have any more questions, or suggestions, feel free to join the Kanna Protecc discord:
https://discord.gg/SyZcuTPXZA
