# VisAssets

 VisAssets is a visualization framework for Unity.  
 It enable us to construct visualization application by connecting 
 visualization modules on the hierarchy window of Unity.

![VisAssets Simulation View](images/image-0.png)

## How to use?

### Verified Environments

- **Unity 2022.3 LTS** (Verified with Unity 2022.3.62f3)

### Installation via Unity Package Manager (UPM)

1. Open your Unity project and navigate to `Window` ＞ `Package Manager`.
2. Click the `+` button in the top-left corner and select `Add package from git URL...`.
3. Enter the following Git URL and click `Add`.
   ```text
   https://github.com/kawaharas/VisAssets.git?path=/Assets/VisAssets
   ```
4. Please ensure the dependency [RuntimeFileBrowser](https://github.com/yasirkula/UnitySimpleFileBrowser) is imported into your project as well.
5. Navigate to `Window` ＞ `TextMeshPro` ＞ `Import TMP Essential Resources` from the top menu to import the required TMP assets. (*If the import dialog pops up automatically via the auto-import script, simply click "Import"*)

Once imported, locate the sample modules in `Packages/VisAssets/Prefabs`, then drag and drop the modules you need into the hierarchy window.

## Connection rules of each modules

- Reading modules can be the parent of filtering modules or mapping modules.
- Filtering modules can be the parent of filtering modules or mapping modules.
- Mapping modules cannot be the parent of either module.

 Module will be deactivated at runtime, if it is not properly connected.

## How to develop new modules?

 You can develop new modules by implementing visualization routines 
 to overriding functions in C# script that inherits from the template class.
 Currently, VisAssets has three types of template classes which are 
 ReadModuleTemplate, FilterModuleTemplate and MapperModuleTemplate.
 These are placed in Packages/VisAssets/Scripts/ModuleTemplates.

 1) Create your own C# script that inherits from the template class.
 2) Create an Empty GameObject.
 3) Attach following scripts and components to the GameObject.

    |  |Activation.cs |DataField.cs |{YourOwnScript}.cs |MeshFilter |MeshRenderer |Material |
    |---|:-:|:-:|:-:|:-:|:-:|:-:|
    |ReadModule   | o | o | o | | | |
    |FilterModule | o | o | o | | | |
    |MapperModule | o | | o | o | o | o |

    Activation.cs and DataField.cs will be automatically attached to the GameObject by the template class, if they are not attach.  
    MeshFilter and MeshRenderer must attach to the MapperModule for rendering visualization results on the scene.
    Material and shader also must set appropriately.

 4) Set Tag of GameObject to "VisModule".
 5) Prefabricate the GameObject.

## Sample modules

|Module name|Function |Base Class |
|---|---|---|
|ReadField |Read ASCII file |ReadModuleTemplate |
|ReadV5 |Read VFIVE data |ReadModuleTemplate |
|ReadGrADS |Read GrADS data |ReadModuleTemplate |
|ReadRAW | Read raw binary files (Headerless / Fortran unformatted sequential format support) |ReadModuleTemplate |
|ReadUncompressedDICOM|Read uncompressed DICOM medical imaging data|ReadModuleTemplate |
|ExtractScalar |Extract an element from the input data |FilterModuleTemplate |
|ExtractVector |Extract 1-3 elements from the input data |FilterModuleTemplate |
|Downsize |Downsize the input data |FilterModuleTemplate |
|VectorMagnitude |Calculate magnitude from vector data as scalar data |FilterModuleTemplate |
|Remap |Grid remapping and data interpolation |FilterModuleTemplate |
|Bounds |Draw a boundingbox of target data |MapperModuleTemplate |
|Outline |Render wireframes of the calculation grid |MapperModuleTemplate |
|Slicer |Draw a colorslice |MapperModuleTemplate |
|Isosurface |Draw an isosurface |MapperModuleTemplate |
|Arrows |Draw vector arrows |MapperModuleTemplate |
|Topo |Render terrain topologies |MapperModuleTemplate |
|VolumeRenderer |3D texture-based volume rendering |MapperModuleTemplate |
|ContourLines |Render isolines (contour lines) for scalar fields |MapperModuleTemplate |
|StreamLines |Render streamlines or ribbons along vector fields (starting points specified interactively)|MapperModuleTemplate |
|ParticleTracer |Render particle pathlines along vector fields (starting points specified uniformly on a plane)|MapperModuleTemplate |
|UIManager |User Interface | |
|Animator |Control of time evolution data | |

## Sample dataset for the sample modules

- for ReadField: include in unitypackage (Assets/StreamingAssets/Sample3D3.txt)
  *Note: When installing via Unity Package Manager, this file is not automatically deployed. Before running the sample scene, please manually copy `Packages/VisAssets/StreamingAssets/Sample3D3.txt` to your project's `Assets/StreamingAssets/Sample3D3.txt` (create the directory if it does not exist).*
  
- for ReadVFIVE: from [here](https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html) (sample_little.tar.gz, sample_big2.tar.gz)  
    module settings for sample_little.tar.gz (dynamo): Precesion: DOUBLE, Byteswap: off, Header: on  
    module settings for sample_big2.tar.gz (ABC flow): Precesion: DOUBLE, Byteswap: on, Header: on  
  
- for ReadGrADS: from [here](http://cola.gmu.edu/grads/) (example.tar.gz)


## Sample Scenes

There are three sample scenes in Assets/VisAssets/Scenes.  
Open them and play on Unity Editor.

- ReadFieldSample.scene
- ReadV5Sample.scene
- ReadGrADSSample.scene

## License

This framework is released under a modified MIT License. 
You are free to use, modify, and redistribute this software for any purpose, **EXCLUDING the right to sell it** (commercial resale is strictly prohibited). For more details, please refer to the [LICENSE](LICENSE) file.

## Citation

 Hideo Miyachi and Shintaro Kawahara,
 ["Development of VR Visualization Framework with Game Engine"](https://www.jstage.jst.go.jp/article/tjsst/12/2/12_59/_article/-char/ja/),
 Transaction of the Japan Society for Simulation Technology, Vol.12, No.2, pp59-67 (2020), doi:10.11308/tjsst.12.59
 *(written in Japanese)*
