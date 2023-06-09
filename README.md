# UnityEditorPngMinify

A Unity Editor script for **lossy** png minification using pngquant.

## Why?

If you store all your textures in Git, and retaining original texture quality isn't a critical requirement, this script can help you reduce the size on disk of your textures.

You really should be considering something like Git LFS otherwise, as Git is not designed for large file storage.

## Install

Tested with Unity 2019.4.31f1 on Windows only.

### Via VRChat Creator Companion

Add the following community repository:

```
https://enitama.github.io/vpm-repos/vpm.json
```

The "PngMinify" package should become available for you to add to your Unity projects.

### Via .unitypackage

Coming soon.

### Via Git submodule

Run the following in your `Assets` folder:

```
git submodule add https://github.com/enitama/UnityEditorPngMinify.git PngMinify
```

## Usage

Download pngquant and extract it somewhere. Select it from the window and select the directory containing assets you wish to minify.

New assets will be generated. A later version of this script will replace your original assets and create a backup elsewhere.
