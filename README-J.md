# VisAssets

 VisAssets��Unity�p�̉����t���[�����[�N�ł��B  
 Unity�̃q�G�����L�[�E�B���h�E��Ń��W���[����ڑ����邱�Ƃɂ������A�v���P�[�V�������\�z���邱�Ƃ��ł��܂��B


## �ȒP�Ȏg����

 Assets/VisAssets/Prefabs���̃T���v�����W���[������K�v�ȃ��W���[�����q�G�����L�[�E�B���h�E�Ƀh���b�O�A���h�h���b�v���܂��B

## ���W���[���Ԃ̐ڑ����[��
- �f�[�^�Ǎ��p���W���[���̓t�B���^�����O���W���[���ƃ}�b�s���O���W���[���̐e�ɂȂ邱�Ƃ��ł��܂��B
- �t�B���^�����O���W���[���̓t�B���^�����O���W���[���ƃ}�b�s���O���W���[���̐e�ɂȂ邱�Ƃ��ł��܂��B
- �}�b�s���O���W���[���͂�����̃��W���[���̐e�ɂ��Ȃ邱�Ƃ��ł��܂���B

    �����W���[�����K�؂ɐڑ�����Ă��Ȃ��ꍇ�A���̃��W���[���͎��s���ɔ�A�N�e�B�u������܂��B

## �V���ȃ��W���[���̊J��

 �e���v���[�g�N���X���p������C#�X�N���v�g�̃I�[�o�[���C�h�֐��ɉ������[�`�����������邱�ƂŁA�V���ȃ��W���[�����J�����邱�Ƃ��ł��܂��B����VisAssets�ɂ́AReadModuleTemplate�AFilterModuleTemplate�AMapperModuleTemplate��3��ނ̃e���v���[�g�N���X������܂��B������ Assets/VisAssets/Scripts/ModuleTemplates ���ɂ���܂��B

 1) �e���v���[�g�N���X���p������C#�X�N���v�g���쐬���܂��B
 2) ��̃Q�[���I�u�W�F�N�g���쐬���܂��B
 3) 2)�ō쐬������̃Q�[���I�u�W�F�N�g�Ƀ��W���[����ʂɉ����ĉ��L�̃X�N���v�g����уR���|�[�l���g���A�^�b�`���܂��B

    |  |Activation.cs |DataField.cs |{YourOwnScript}.cs |MeshFilter |MeshRenderer |Material |
    |---|:-:|:-:|:-:|:-:|:-:|:-:|
    |ReadModule   | o | o | o | | | |
    |FilterModule | o | o | o | | | |
    |MapperModule | o | | o | o | o | o |

    Activation.cs��DataField.cs���A�^�b�`����Ă��Ȃ��ꍇ�A�e���v���[�g�N���X�͂����������I�ɃQ�[���I�u�W�F�N�g�ɃA�^�b�`���܂��B 
    �V�[����ŉ������ʂ������_�����O���邽�߁AMeshFilter��MeshRenderer���}�b�s���O���W���[���ɃA�^�b�`����K�v������܂��B
    �܂��A�}�e���A����V�F�[�_�ɂ��Ă��K�؂ɐݒ肷��K�v������܂��B

 4) �Q�[���I�u�W�F�N�g�̃^�O���� "VisModule" �ɕύX���܂��B
 5) �Q�[���I�u�W�F�N�g���v���n�u�����܂��B

## �T���v�����W���[��

|���W���[����|�@�\ |���N���X |
|---|---|---|
|ReadField |�e�L�X�g�t�@�C���̓ǂݍ��� |ReadModuleTemplate |
|ReadV5 |VFIVE�p�f�[�^�̓ǂݍ��� |ReadModuleTemplate |
|ReadGrADS |GrADS�p�f�[�^�̓ǂݍ��� |ReadModuleTemplate |
|ExtractScalar |���̓f�[�^����̒P�ꐬ���̒��o |FilterModuleTemplate |
|ExtractVector |���̓f�[�^�����1�`3�����̒��o |FilterModuleTemplate |
|Interpolator |���}��Ԃɂ��_�E���T�C�Y |FilterModuleTemplate |
|Bounds |�f�[�^�̋��E���̕`�� |MapperModuleTemplate |
|Slicer |�f�ʐ}�̕`�� |MapperModuleTemplate |
|Isosurface |���l�ʂ̕`�� |MapperModuleTemplate |
|Arrows |�x�N�g�������ŕ\�� |MapperModuleTemplate |
|UIManager |���[�U�C���^�t�F�[�X | |
|Animator |���Ԕ��W�f�[�^�̃R���g���[�� | |

## �T���v�����W���[���̃e�X�g�ɗp�����f�[�^�Z�b�g

- �e�L�X�g�t�@�C��: �{�p�b�P�[�W�ɓ��� (sample3D3.txt)
- VFIVE�p�f�[�^: https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html ���擾 (sample_little.tar.gz)
- GrADS�p�f�[�^: http://cola.gmu.edu/grads/ ���擾 (example.tar.gz)


## �T���v���A�v���P�[�V����

�T���v���A�v���P�[�V������ Assets/VisAssets/Scenes �ɂ���܂��B
�ǂݍ��݌�AUnity�G�f�B�^��Ŏ��s���Ă��������B

- ReadFieldSample.scene
- ReadV5Sample.scene
- ReadGrADSSample.scene

## Citation

 �{�n�p��, �쌴�T���Y, 
 ["�Q�[���G���W����p����VR�����t���[�����[�N�̊J��"](https://www.jstage.jst.go.jp/article/tjsst/12/2/12_59/_article/-char/ja/), 
 ���{�V�~�����[�V�����w��_����, Vol.12, No.2, pp59-67 (2020), doi:10.11308/tjsst.12.59
