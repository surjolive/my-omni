# Third-Party Notices

The native Windows executable embeds the following runtime components. Their upstream licenses and notices are included in the distributed runtime where applicable.

## Node.js

The build embeds the Node.js executable found on the build machine. Node.js is distributed under the MIT license and includes third-party components; see the Node.js license and `THIRD_PARTY_NOTICES.txt` from the official Node.js distribution matching the embedded runtime version.

## npm

The build embeds the npm CLI and its dependency tree from the npm installation next to the build machine's Node.js executable. npm is distributed under the Artistic License 2.0. Its package and dependency license files are included in the embedded npm archive.

## OmniRoute

OmniRoute is not bundled. The app downloads the pinned npm package `omniroute@3.8.53` only after user approval at startup or when the user chooses **Install / Start OmniRoute**. OmniRoute is licensed under MIT; its package and dependency notices are installed with it in the user's local application data directory.

## Microsoft .NET Framework

The Windows app uses system-provided .NET Framework Windows Forms, networking, compression, and DPAPI APIs. .NET Framework is a Microsoft component and is not copied into the executable.