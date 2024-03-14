# PuppetMaster Fork

This fork is intended to add a few features the original plugin is missing.
Those features include :
- Being able to enable or disabled the plugin with a checkbox
- Being able to enable a whitelist and a blacklist (And organize entries to those lists with dragging and dropping)
- Being able to add specific settings per whitelisted players or to make them use the default settings

Dev Changelog :
- Preparation for Dalamud API level 10 (IPlugginLogger)
- Cleaned decompiled code

# How to Use - READ EVERYTHING FIRST

IMPORTANT : Make sure to backup your original puppetmaster trigger phrases/regex as you will probably lose them while switching to this version !

As long as DodingDaga does not accept my pull request and merge my version into theirs, you will have to first disable the original plugin, delete it from /xlplugins and then add the custom repo given below to start downloading the new version 

YOU DO NOT NEED TO DELETE DODINGDAGA'S REPO, IF YOU USE COPYCAT, KEEP IT

`https://raw.githubusercontent.com/Aspher0/PuppetMaster_Fork/main/PuppetMaster.json`

# Credits

Thanks to DodingDaga for this awesome plugin.

Credits to the SimpleTweaksPlugin by Caraxi for providing the ChatHelper.cs file.
You can find the SimpleTweaks source code here : https://github.com/Caraxi/SimpleTweaksPlugin

Edit : Original ChatHelper.cs file found here
https://git.anna.lgbt/ascclemens/XivCommon/src/branch/main/XivCommon/Functions/Chat.cs
