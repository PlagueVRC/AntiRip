The official root repository for this project is here https://github.com/rygo6/GTAvaCrypt. Only download from a fork if you specifically trust that fork and creator!

**This repository is from a direct co-developer of the official root repository who is maintaining the updates. It is up to you if you want to use this repo.**

# Kanna AntiRip

This is a rather invasive anti-avatar-ripping system to be used for VRChat. It will protect against your avatar being ripped, extracted and edited. It will also protect against your avatar being ripped and re-uploaded without edits.

This system will randomize all the vertices of your avatar's mesh, then write that to disk. Then rely on a custom shader with a 32 bit key to un-randomize the vertex positions in game. This is <b>not</b> done through blend shapes. Rather this will copy, and destructively edit, the 'Basis' layer of your mesh.

Technically this is more like 'Avatar Obfuscation' but calling it 'AvaObfs' didn't really have the same ring to it. But maybe it will keep evolving to use some advanced GPU encryption techniques some day...

1. [Caveats of this System](#caveats-of-this-system)

2. [Usage Instructions](#usage-instructions)

3. [Features](#features)

4. [How secure is this?](#how-secure-is-this)

## Caveats of this System

1. For a user to see your avatar properly in VRChat, they must have your avatar fully shown. Shaders, animations and all. So this system is only ideal for avatars you use in worlds where most people are your friends.

2. This synchronizes the key with Avatar 3.0 parameters and does take up 32 bits. So this system can only work with avatars that use the VRChat Avatar 3.0 SDK.

3. Shaders must be manually edited to work with AvaCrypt. Currently this system will inject itself into the official Poiyomi 8/8.1 Toon Shader when you click "Encrypt Mesh". Any material which is not Poiyomi 8/8.1 will simply be ignored.

<i>This could be adapted to other shaders but I really don't have time to port it to every shader out there, so I will only support Poiyomi for now. I am open to people contributing in this area. If enough offer to help, I will make this a modular system.</i>

## Usage Instructions

### Backup your project before running these operations in case it doesn't work properly and causes difficult to fix, or impossible to fix, changes in your project.

#### Really do it. Close Unity, all your programs, and make a full clean copy of your entire Unity Project folder. A small percentage of avatars did have odd things in their mesh that just wouldn't work, or could cause errors, and the script could leave some assets in the project in a rather messed up state.

#### Install AvaCrypt and Poiyomi.

1. Ensure you are using latest VRC SDK.
2. Download the Poiyomi 8/8.1 package from https://github.com/poiyomi/PoiyomiToonShader or Poiyomi's patreon discord if you bought, and import it into your Unity project.
3. Click on Code -> download zip on this repo. Once downloaded, extract it. Once you have the folder, put that into your assets folder of your unity project.

#### Setup VRC Components.

1. Add the `AvaCryptV2Root` component onto the root GameObject of your avatar, next to the `VRCAvatarDescriptor` component.

![Steps 1](Textures/DocSteps1.png)

2. Ensure your `VRCAvatarDescriptor` has an AnimatorController specified in the 'FX Playable Layer' slot. <b>The AnimatorController you specify should not be shared between multiple avatars, AvaCrypt is going to write states into the controller which will need to be different for different avatars.</b>
3. Ensure there is also an `Animator` component on this root GameObject, and that its 'Controller' slot points to the same AnimatorController in the 'FX Playable Layer' slot on the `VRCAvatarDescriptor`.

![Steps 1](Textures/DocSteps2to3.png)

5. In the 'Parameters' slot of your `VRCAvatarDescriptor` ensure you have an 'Expression Parameters' object.

![Step 4](Textures/DocSteps4.png)

#### Delete your old Un-Encrypted Avatar from VRC Backend!

VRC API stores old uploads of your avatar! So if you start uploading an encrypted avatar with an ID that you previously uploaded non-encrypted, it may entirely negate any benefit this provides as rippers can just download an older version that was not encrypted.

1. Go into the VRChat SDK Inspector in the Unity Editor, then under 'Content Manager' find the avatar you wish to protect and delete it entirely from the VRC backend.
2. Go to your current avatar's `Pipeline Manager` component and click the `Detach (Optional)` button so it will generate a new avatar id on upload.

#### Encrypting and Uploading

1. Ensure any meshes you wish to have encrypted are using Poiyomi 8/8.1. It will skip over meshes that do not use this shader.
2. On the `AvaCryptV2Root` component click the 'Encrypt Avatar' button. This will lock all of your Poiyomi materials, make all necessary edits to your AnimatorController, and make a duplicate of your avatar which is encrypted. Be aware your duplicated avatar with "_Encrypted" appended to it's name will appear completely garbled in the editor. This is what other users will see if they do not have your avatar shown. *Do not set the keys on the material inside the Unity Editor.*
3. Go to the VRChat SDK Menu then 'Build and Publish' your avatar which has '_Encrypted' appended to the name.

*I found some Poi 8/8.1 materials get into a weird state with Lock/Unlock and AvaCrypt can't lock them. If you get errors that say something like 'Trying to Inject not-locked shader?!' go to the Poi 8/8.1 material it is complaining about and manually click the Lock/Unlock button to get it out of its weird state.*

#### Writing Keys

1. After upload completes, go to the GameObject of your encrypted avatar. Find the `Pipeline Manager` component and copy it's blueprint ID. Then paste the blueprint ID into the `Pipeline Manager` on the un-encrypted avatar and click 'Attach'. This is important.
2. Now on the AvaCryptV2Root component click the 'Write Keys' button. Ensure VRC is closed when you do this, as VRC might disallow writing to the file. This will actually read in and alter the saved 3.0 parameters from your VRChat folder to include the new key so you don't have to enter them in-game. <i>This also means if you "Reset Avatar" in game through the 3.0 menu, it will reset your keys and you will need to re-export them with the 'Write Keys' button!</i>
3. This should provide ample error dialogues or console errors, so ensure no errors came up!. It should popup a success dialogue if it did it correctly. If there were issues make sure the 'Vrc Saved Params Path' actually points to your LocalAvatarData folder.
4. You only need to run 'Write Keys' once on first setup, or when you change keys.

*Ensure VRChat is closed when you write keys otherwise VRChat may just overwrite them immediately with zeroes!

#### Un-Encrypting Poiyomi Material in Editor

If you wish to see your avatar again as normal and not encrypted, unlock all of your Poiyomi materials. AvaCrypt only writes itself into the locked Poiyomi shader files, so you can fully turn it off just by unlocking the materials again. AvaCrypt has a utility for this. Click on your original non encrypted avatar and click unlock poi mats.

If you do unlock any of the Poiyomi materials you will need to click the 'Encrypt Avatar' button again before uploaded, as it is during that process that it will inject itself into the locked Poiyomi shaders.


If you have any more questions, or suggestions, feel free to join the AntiRip discord:
https://discord.gg/SyZcuTPXZA

## How secure is this?

I will keep transparent here without guiding rippers on how to attack your works. This is not foolproof, but close to the best you can get at this time. Rip wise, this cannot be currently ripped without a insane amount of work, as ripping compiled shadercode, reversing it back to unity compatible code and also getting hold of the keys would be hell. Hotswap wise, a dedicated enough ripper with experience with mods could hotswap your avatar. This is not immune to that. You can however put a watermark on your avatar to drive hotswappers into being banned which they wont be able to remove, as the meshes will be encrypted, regardless of hotswap. A hotswap done with the high amount of work i have mentioned here would only get the avatar working normally in game; not in unity.
