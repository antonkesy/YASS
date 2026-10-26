# Changelog

## [1.3.0](https://github.com/antonkesy/YASS/compare/v1.2.2...v1.3.0) (2026-10-26)


### Features

* **android:** add the Android host ([a9cf40d](https://github.com/antonkesy/YASS/commit/a9cf40d866f62a63803b2a91a1e542225135c68b))
* **android:** Android app for network scanners ([736803a](https://github.com/antonkesy/YASS/commit/736803ac4cac9c61eab6c4dfcba8e5941d06a75b))
* **android:** share a finished document ([7849f0a](https://github.com/antonkesy/YASS/commit/7849f0a9dba9717d60a6b3de109dbb6e09ab82d6))
* **scanning:** auto means eSCL on Android ([ca0fe33](https://github.com/antonkesy/YASS/commit/ca0fe33d5eb12476d3b0ac95258e1e82ad89f008))
* **web:** share button in the Android app ([11a332b](https://github.com/antonkesy/YASS/commit/11a332bb896f52a8a3a1d71adad06f2c300d70e6))
* **web:** talk to the Android host over its message channel ([bb00fec](https://github.com/antonkesy/YASS/commit/bb00fec13363999b7b5cea49318fb580eb4047ff))


### Bug Fixes

* **android:** give the activity a stable name ([ee80e52](https://github.com/antonkesy/YASS/commit/ee80e5294487a5af549be17e5e7bfb6c311c460b))
* **android:** run on CoreCLR instead of Mono ([d62cba2](https://github.com/antonkesy/YASS/commit/d62cba2f7754ce9cf63b4ae7164f1aa2724c6388))


### Documentation

* document the Android app ([809cd0b](https://github.com/antonkesy/YASS/commit/809cd0b7bcb136f8942fe40f831aea6981528922))

## [1.2.2](https://github.com/antonkesy/YASS/compare/v1.2.1...v1.2.2) (2026-10-18)


### Bug Fixes

* **web:** centre the scanner pills beside Look for scanners ([d9bbbec](https://github.com/antonkesy/YASS/commit/d9bbbeca547ceb63c6e9b0adb028dbd5e9136079))
* **web:** scroll a full home stage instead of squeezing its rows ([6acde75](https://github.com/antonkesy/YASS/commit/6acde758b6c48b44b745c05e480ad60b145d28ef))

## [1.2.1](https://github.com/antonkesy/YASS/compare/v1.2.0...v1.2.1) (2026-10-18)


### Bug Fixes

* **web:** fit Look for scanners into the home stage without a new row ([877fc27](https://github.com/antonkesy/YASS/commit/877fc27c2be2c94b420974ca52f6c5753e50167f))
* **web:** make the drag ghost's zoom reactive ([0c0dbda](https://github.com/antonkesy/YASS/commit/0c0dbdabee7e6f0e56d90d0d41d0c02b70ef4a00))

## [1.2.0](https://github.com/antonkesy/YASS/compare/v1.1.0...v1.2.0) (2026-10-18)


### Features

* **app:** add a RefreshDevices hub call ([cb8e542](https://github.com/antonkesy/YASS/commit/cb8e5428040e03e028e078fd89cffc02a00c805b))
* **desktop:** fill in the save dialog's name and format, with an optional name template ([f7428e9](https://github.com/antonkesy/YASS/commit/f7428e9d01e77cce8d9739d144e73c2542de7657))
* **desktop:** stop the webview's browser shortcuts ([438c099](https://github.com/antonkesy/YASS/commit/438c099b68952f3e30e9281b84e268350d11d396))
* **scanning:** let a device listing skip the enumeration cache ([3e7cf82](https://github.com/antonkesy/YASS/commit/3e7cf82c70511d47ac7cc9298f52702aaf6a0a81))
* **web:** add a Look for scanners button ([e719f9d](https://github.com/antonkesy/YASS/commit/e719f9d5b083157d454cc094c2e08576a2f20c2c))
* **web:** hint at missing drivers in the empty scanner list ([7ee4657](https://github.com/antonkesy/YASS/commit/7ee4657c9bb20df9e50d515bfe52767be33b0e53))
* **web:** hint that a scanner not found may need its driver ([22f67d6](https://github.com/antonkesy/YASS/commit/22f67d64d6c161c8f7152b6262e6be32f5a4a2f4))
* **web:** scale the whole UI up on big screens ([b138efd](https://github.com/antonkesy/YASS/commit/b138efd19b3898fe25f24d16aee07a408eefa1e1))


### Bug Fixes

* **web:** change logo palette to blue ([9d3cc34](https://github.com/antonkesy/YASS/commit/9d3cc34305839ea7b4d013f0bc9f283904668b1d))
* **web:** drop the gap under PDF properties in WebKit ([ff72b94](https://github.com/antonkesy/YASS/commit/ff72b9441a438908af354f94f971f11b63e71d3c))
* **web:** keep middle and modified link clicks in the desktop window ([1e6ef64](https://github.com/antonkesy/YASS/commit/1e6ef648236185b8d9f3ac0fc9a476c7d7b3c234))
* **web:** keep the desktop app's labels and buttons from selecting like text ([d43b940](https://github.com/antonkesy/YASS/commit/d43b9405b329c1d12ec7b06051f85b91e9abf28a))
* **web:** let nothing be dragged out of or dropped into the desktop app ([c3d9e9f](https://github.com/antonkesy/YASS/commit/c3d9e9f9c59fe4906541194106a04d83a4319eb0))
* **web:** let the review and home panes reach the window edges ([4a72516](https://github.com/antonkesy/YASS/commit/4a725167e823fa294f66fa6f620eb25009bd1cf9))
* **web:** say "saved" instead of "downloaded" in the scans overview ([e758254](https://github.com/antonkesy/YASS/commit/e758254f54e6d658f0786d694a1e8594f9c5411e))
* **web:** stop WebView2's other browser shortcuts in the desktop app ([ddf1062](https://github.com/antonkesy/YASS/commit/ddf1062cbaa3d9098e66db5088b4c3c77741f170))


### Documentation

* list what the desktop window stops of a browser's behaviour ([ebbbe96](https://github.com/antonkesy/YASS/commit/ebbbe962bf80a46c6742363f3500b53aaa088e7e))

## [1.1.0](https://github.com/antonkesy/YASS/compare/v1.0.0...v1.1.0) (2026-10-11)


### Bug Fixes

* **desktop:** open the Windows save dialog in the suggested folder ([762cf8a](https://github.com/antonkesy/YASS/commit/762cf8a520d2dcc2c0819f2ca00f6fd9685434ed))


### Maintenance

* release 1.1.0 ([1487d4e](https://github.com/antonkesy/YASS/commit/1487d4e55589a3c02a0fef6dd09a6af0d93200b7))

## 1.0.0 (2026-10-11)

First stable release.
