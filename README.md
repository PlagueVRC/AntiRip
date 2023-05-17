The official root repository for this project is here https://github.com/rygo6/GTAvaCrypt. Only download from a fork if you specifically trust that fork and creator!

# GeoTetra AvaCrypt V2

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

3. Shaders must be manually edited to work with AvaCrypt. Currently this system will inject itself into the official Poiyomi 8 ToonShader when you click "Encrypt Mesh". Any material which is not Poiyomi 8 will simply be ignored.

 <i>This could be adapted to other shaders but I really don't have time to port it to every shader out there, so I will only support the latest Poiyomi for now.</i>

## Usage Instructions

### Backup your poject before running these operations in case it doesn't work properly and causes difficult to fix, or impossible to fix, changes in your project.

#### Really do it. Close Unity, all your programs, and make a full clean copy of your entire Unity Project. Or better yet, learn to use git. A small percentage of avatars did have odd things in their mesh that just wouldn't work, or could cause errors, and the script could leave some assets in the project in a rather messed up state.

#### Upgrading from V2.2.1 Onwards...

1. If you installed via Unity Package Manager click `Tools > GeoTetra > GTAvaCrypt > Check for Update...`.

If you didn't install via Unity Package Manager, delete and re-import. But should use Unity Package Manager, makes it easier and keeps your Assets folder cleaner.

If you have a version older than 2.2.1 read the ["Old Installation" instructions](OLD_INSTALLATION.md).

#### Install AvaCrypt and Poiyomi.

1. Ensure you are using latest VRC SDK. I would personally recommend using the "VRChat Creator Companion Beta" to do this as it makes a number of things simpler. https://vrchat.com/home/download
2. Download the Poiyomi 8 package from https://github.com/poiyomi/PoiyomiToonShader and import it into your Unity project.
3. In the Unity Editor click `Window > Package Manager`. Then in the Package Manager window click the `+` in the upper left corner and select `Add package from git url...` and then paste `https://github.com/rygo6/GTAvaCrypt.git` in the field and click `Add`. This will clone the package via the Package Manager into the Package folder. **If you get an error about git not being installed, you may need to install the git package from here: https://git-scm.com**. *Don't be afraid of git, the Unity Package Manager completely abstracts it away*.

#### Setup VRC Components.

1. Add the `AvaCryptV2Root` component onto the root GameObject of your avatar, next to the `VRCAvatarDescriptor` component.

![Steps 1](Textures/DocSteps1.png)

2. Ensure your `VRCAvatarDescriptor` has an AnimatorController specified in the 'FX Playable Layer' slot. <b>The AnimatorController you specify should not be shared between multiple avatars, AvaCrypt is going to write states into the controller which will need to be different for different avatars.</b>
3. Ensure there is also an `Animator` component on this root GameObject, and that its 'Controller' slot points to the same AnimatorController in the 'FX Playable Layer' slot on the `VRCAvatarDescriptor`.

![Steps 1](Textures/DocSteps2to3.png)

5. In the 'Parameters' slot of your `VRCAvatarDescriptor` ensure you have an 'Expression Parameters' object.

![Step 4](Textures/DocSteps4.png)

6. <i>Optional V1 Cleanup step.</i> If you have AvaCrypV1 installed, go to 'AvaCryptV2Root' component and under the 'Debug' foldout at the bottom click the button that says 'Delete AvaCryptV1 Objects From Controller'. This should delete all the old AvaCryptV1 layers and blend trees. But still go into the FX AnimatorController and delete any old AvaCrypt keys or layers you see. You can also delete all the 'AvaCryptKey0' 'AvaCryptKey100' animation files it previously generated next to your controller.

#### Delete your old Un-Encrypted Avatar from VRC Backend!

VRC API stores old uploads of your avatar! So if you start uploading an encrypted avatar with an ID that you previously uploaded non-encrypted, it may entirely negate any benefit this provides as rippers can just download an older version that was not encrypted.

1. Go into the VRChat SDK Inspector in the Unity Editor, then under 'Content Manager' find the avatar you wish to protect and delete it entirely from the VRC backend.
2. Go to your current avatar's `Pipeline Manager` component and click the `Detach (Optional)` button so it will generate a new avatar id on upload.

#### Encrypting and Uploading

1. Ensure any meshes you wish to have encrypted are using Poiyomi 8. It will skip over meshes that do not use this shader.
2. On the `AvaCryptV2Root` component click the 'Encrypt Avatar' button. This will lock all of your Poiyomi materials, make all necessary edits to your AnimatorController, and make a duplicate of your avatar which is encrypted. Be aware your duplicated avatar with "_Encrypted" appended to it's name will appear completely garbled in the editor. This is what other users will see if they do not have your avatar shown. *Do not set the keys on the material inside the Unity Editor.*
3. Go to the VRChat SDK Menu then 'Build and Publish' your avatar which has '_Encrypted' appended to the name.

*I found some Poi 8 materials get into a weird state with Lock/Unlock and AvaCrypt can't lock them. If you get errors that say something like 'Trying to Inject not-locked shader?!' go to the Poi 8 material it is complaining about and manually click the Lock/Unlock button to get it out of its weird state.*

#### Writing Keys

1. If this is the first time you have uploaded this avatar, after upload completes, go to the GameObject of your encrypted avatar. Find the `Pipeline Manager` component and copy it's blueprint ID. Then paste the blueprint ID into the `Pipeline Manager` on the un-encrypted avatar and click 'Attach'.
2. Now on the AvaCryptV2Root component click the 'Write Keys' button. Ensure VRC is closed when you do this, as VRC might disallow writing to the file. This will actually read in and alter the saved 3.0 parameters from your VRChat folder to include the new key so you don't have to enter them in-game. <i>This also means if you "Reset Avatar" in game through the 3.0 menu, it will reset your keys and you will need to re-export them with the 'Write Keys' button!</i>
3. This should provide ample error dialogues or console errors, so ensure no errors came up!. It should popup a success dialogue if it did it correctly. If there were issues make sure the 'Vrc Saved Params Path' actually points to your LocalAvatarData folder.
4. You only need to run 'Write Keys' once on first setup, or when you change keys.

*Ensure VRChat is closed when you write keys otherwise VRChat may just overwrite them immediately with zeroes! ~thanks Meru*

#### Un-Encrypting Poiyomi Material in Editor

If you wish to see your avatar again as normal and not encrypted, unlock all of your Poiyomi materials. AvaCrypt only writes itself into the locked Poiyomi shader files, so you can fully turn it off just by unlocking the materials again.

If you do unlock any of the Poiyomi materials you will need to click the 'Encrypt Avatar' button again before uploaded, as it is during that process that it will inject itself into the locked Poiyomi shaders.


If you have any more questions, or suggestions, feel free to join the GeoTetra discord:
https://discord.gg/nbzqtaVP9J

## Features

### New Features Of Version 2.2.9:

1. Will obfuscate disable GameObjects.
2. Will ignore meshes being used for cloth.

### New Features Of Version 2.2.8:

1. Added 'Additional Materials' list that lets you specify additional materials to have the AvaCrypt code injected into when you click 'EncryptAvatar'. This will let you encrypt materials used in material swaps.
2. Made it so the obfuscated mesh/material does not show unless the shaders are fully shown. If your whole avatar is obfuscated then other users will see nothing until they show you.
3. Added `GeoTetra/UnlitHideWhenShown` shader that will be visible when your avatar is not shown, but hidden when it is shown. This will let placeholder geometry+texture be visible when the avatar is not shown. I included a prefab called `HideWhenShownQuad` which is a quad with a texture that says 'Avatar only visible when fully shown' with this shader on it. Drag this prefab onto your hip bone for a quick to add placeholder, or make whatever placeholder you want.
4. Fix for some animator controller layers potentially getting messed up. 
5. Added a 'Delete AvaCrypt Objects From Controller' button under the Debug foldout at the bottom of AvaCryptV2Root component. This will fully flush all avacrypt layers and states from the controller if for some reason your controller gets in a weird state or just want to delete the avacrypt stuff.

### New Features Of Version 2.2.5:

Plethora of bug fixes.
1. It was possible it could edit the Poiyomi 8 shader before! This is fixed now. If it did this, reimport Poi 8 package.
2. Double checks more of the animator states to ensure they are properly configured.
3. Now puts all of the BitKey AnimationClips in a 'BitKeyClips' folder when generating them. If you want to have it move these files automatically, delete all `Avatar_BitKey0_False.anim` animation clips in your Project assets, it should regenerate them properly in the new folder and hook them up.

### New Features Of Version 2.2.2:

1. Added ignored materials list on AvaCryptV2Root. Be aware if you add a material to this list then any mesh which uses that material will also have all the other materials it uses ignored as well.

### New Features Of Version 2.2.1:

1. Added option under `Tools > GeoTetra > GTAvaCrypt > Unlock All Poi Materials In Hierarchy...` to unlock all Poiyomi 8 materials under a selected GameObject. *~thanks Meru for suggestion*
2.  Added option under `Tools > GeoTetra > GTAvaCrypt > Check for Update...` to automatically update the package if one is available to make future updates easier.

### New Features Of Version 2.2:

Upgraded to work with Poiyomi 8, fixed the terrible workflow with Poiyomi shader and can now be installed through Unity Package Manager.

1. The GTPoiyomiToon fork has now been made obsolete and this works with the official Poiyomi 8 package.
2. You no longer have to right-click on the 'BitKeys' to mark them animatable. The bitkeys aren't even material properties anymore.
3. AvaCrypt will "inject" its code into the locked PoiyomiShader when you click "EncryptAvatar" on the AvaCryptV2Root. It does not alter the unlocked PoiyomiShader. So if you want to turn off the AvaCrypt obfuscation to see your mesh again, just unlock the PoiyomiToon material.

### New Features Of Version 2:
1. You no longer have to input keys into the Avatar 3.0 menu, the package will write the keys to the saved 3.0 avatar parameters file in your VRChat data folder.

2. The keys are now stored and transferred as 32 separate 1-bit bools. It still takes up 32 bits in parameter data, but now it fully utilizes all 32 bits, as before it effectively used maybe 20 of those bits. Before someone would have to brute force a maximum of 1,185,921 combinations. Now they would have to brute force a maximum of 4,294,967,296 combinations. If each brute force attempt took 0.1 seconds it would take over 13 years to brute force, people could put a small compute farm to do this in a number of days, but rarely will someone care to spend the money to do so. This means if you never take your avatar into a public lobby where someone could use a mod to read your keys, it is quite secure.

3. The mesh obfuscation math is now randomly generated and written into the shader each time you encrypt a mesh. What this means is just having the keys is no longer enough to decrypt an avatar. One would also have to decompile and reverse engineer the shader from the asset bundle itself. Currently this makes avatars un-rippable with any currently released tools for VRChat avatar ripping. As Unity 2019 changed the way the shaders get packed into an AssetBundle, now you can only get compiled shader bytecode out of the AssetBundle and no public tool has implemented functionality to do so. If someone were to rip an avatar with this they'd first need to implement a system to decompile the shader bytecode out of an asset bundle, analyze it to determine where the obfuscation math is, then reverse engineer that into C#, or other, to decrypt the mesh. Making such a shader decompiling and transpiling system is a significant difficulty for someone knowledgeable.

## How secure is this?

I try to be as up-front as possible about how exactly you would undo AvaCrypt and how secure it is. I feel comfortable saying it is currently the most difficult anti-ripping scheme to recover usable assets from, and I have a chain of ideas to keep it as such. But it is still possible someone could undo it. This is why I release it as free and MIT. I know some percentage will inevitably have their avatar ripped with this on it if there is enough reason to incentivize rippers to do so. I have some ideas to make something I believe would be secure enough to justify a price, but that may still be a number of months away.

Where this scheme is most secure is in private instances with friends you trust. If you are never in an instance with a ripper, and they try to rip your avatar entirely off the VRChat API, they would have to brute force the 32-bit key. Doing so could take months depending. It would probably be faster and cheaper for them to just remodel it off references.

Currently the biggest source of exploit is that someone can read your "BitKeys" with a mod if you are in the same instance as them. For this reason I do not recommend people run around in public instances with this applied to their avatar if they really wish to keep the avatar protected.

However, even if someone does read your BitKeys with a mod and pulls your avatar's data file, undoing the encryption is still a pain even with the BitKeys. I still have yet to see anything exist that does it automatically. Someone would have to decompile the shader to extract the randomized algorithm in it, then implement that in reverse to undo the encryption with the BitKeys. If some ripper does manage to automate this, I already have something else lined up to pre-occupy them for many more months. I can keep rippers busy for **years**. :)
