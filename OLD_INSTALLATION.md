
#### Upgrading from V2.2 to V2.2.1

Hopefully this is the last time you have to do anything but click a single button to upgrade as I now have a button in the Tools menu to click to automatically do this in the future.

Just remove the old one from the Package Manager and add it again.

1. In the Unity Editor click `Window > Package Manage` then in the `In Project` section find GTAvaCrypt, select it and click `Remove` in the lower right.
2. In the Package Manager window click the `+` in the upper left corner and select `Add package from git url...` and then paste `https://github.com/rygo6/GTAvaCrypt.git` in the field and click `Add`.

*If you get an error about git not being installed, you may need to install the git package from here: https://git-scm.com/*

#### Upgrading from V2 to V2.2

Upgrade should be relatively painless and not break anything. Future upgrades from here should be even simpler as it no longer uses an altered poiyomi and installs through the package manager.

1. Delete the entire GTPoiyomiToon folder and import the official package from https://github.com/poiyomi/PoiyomiToonShader.
2. Delete the old GTAvaCrypt folder.
3. In the Unity Editor click `Window > Package Manager`. Then in the Package Manager window click the `+` in the upper left corner and select `Add package from git url...` and then paste `https://github.com/rygo6/GTAvaCrypt.git` in the field and click `Add`. This will clone the package via the Package Manager into the Package folder.

*If you get an error about git not being installed, you may need to install the git package from here: https://git-scm.com/*

#### Upgrading from V1

If you are upgrading from V1 you will want to clear out everything previously related. This is not a small delta change, many things are fundamentally changed. Also it is made to work with the latest Poiyomi which has also introduced significant changes since V1.

1. Select your avatar and delete the AvaCryptRoot V1 component.
2. Delete the AvaCrypt key entries from your VRCExpressionParameters.
3. Remove the AvaCrypt key menu from VRCExpressionsMenu.
4. Delete the entire GTAvaCrypt folder and the entire GTPoiyomiShader folder. Please note that this did upgrade to use the latest Poiyomi, so when you pull in the new Poiyomi all your shader refs will be broken! But if you go to each material and select the new version under '.poiyomi/PoiyomiToon' it should repopulate the new shader with however you had it configured previously.
5. After you install the new packages, there is a new button on the AvaCryptV2Root component under the 'Debug' foldout at the bottom of it that says 'Delete AvaCryptV1 Objects From Controller'. This should delete all the old AvaCryptV1 layers and blend trees. But still go into the FX AnimatorController and delete any old AvaCrypt keys or layers. You can also delete all the 'AvaCryptKey0' 'AvaCryptKey100' animation files it generated next to your controller.
