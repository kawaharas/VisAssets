# VisAssets

 VisAssets is a visualization framework for Unity.  
 It enable us to construct visualization application by connecting 
 visualization modules on the hierarchy window of Unity.
 
## How to use?

 The First step is to import unitypackage into your Unity project.  
 Then, you can find the sample modules in Assets/VisAssets/Prefabs.  
 From there, drag and drop the modules you need into the hierarchy window.
 
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
 These are placed in Assets/VisAssets/Scripts/ModuleTemplates.

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
|ExtractScalar |Extract an element from the input data |FilterModuleTemplate |
|ExtractVector |Extract 1-3 elements from the input data |FilterModuleTemplate |
|Interpolator |Downsize the input data |FilterModuleTemplate |
|Bounds |Draw a boundingbox of target data |MapperModuleTemplate |
|Slicer |Draw a colorslice |MapperModuleTemplate |
|Isosurface |Draw an isosurface |MapperModuleTemplate |
|Arrows |Draw vector arrows |MapperModuleTemplate |
|UIManager |User Interface | |
|Animator |Control of time evolution data | |

## Sample dataset for the sample modules

- ASCII data: include in unitypackage (sample3D3.txt)
- VFIVE data: from https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html (sample_little.tar.gz)
- GrADS data: from http://cola.gmu.edu/grads/ (example.tar.gz)


## Sample Scenes

There are three sample scenes in Assets/VisAssets/Scenes.  
Open them and play on Unity Editor.

- ReadFieldSample.scene
- ReadV5Sample.scene
- ReadGrADSSample.scene

## Citation

 Hideo Miyachi and Shintaro Kawahara,
 "Development of VR Visualization Framework with Game Engine",
 Transaction of the Japan Society for Simulation Technology, Vol.12, No.2, pp59-67 (2020), doi:10.11308/tjsst.12.59
 *(written in Japanese)*
