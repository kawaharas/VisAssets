# VisAssets

 VisAssetsはUnity用の可視化フレームワークです。  
 Unityのヒエラルキーウィンドウ上でモジュールを接続することにより可視化アプリケーションを構築することができます。


## 簡単な使い方

 Assets/VisAssets/Prefabs内のサンプルモジュールから必要なモジュールをヒエラルキーウィンドウにドラッグアンドドロップします。

## モジュール間の接続ルール
- データ読込用モジュールはフィルタリングモジュールとマッピングモジュールの親になることができます。
- フィルタリングモジュールはフィルタリングモジュールとマッピングモジュールの親になることができます。
- マッピングモジュールはいずれのモジュールの親にもなることができません。

    ※モジュールが適切に接続されていない場合、そのモジュールは実行時に非アクティブ化されます。

## 新たなモジュールの開発

 テンプレートクラスを継承したC#スクリプトのオーバーライド関数に可視化ルーチンを実装することで、新たなモジュールを開発することができます。現在VisAssetsには、ReadModuleTemplate、FilterModuleTemplate、MapperModuleTemplateの3種類のテンプレートクラスがあります。これらは Assets/VisAssets/Scripts/ModuleTemplates 内にあります。

 1) テンプレートクラスを継承したC#スクリプトを作成します。
 2) 空のゲームオブジェクトを作成します。
 3) 2)で作成した空のゲームオブジェクトにモジュール種別に応じて下記のスクリプトおよびコンポーネントをアタッチします。

    |  |Activation.cs |DataField.cs |{YourOwnScript}.cs |MeshFilter |MeshRenderer |Material |
    |---|:-:|:-:|:-:|:-:|:-:|:-:|
    |ReadModule   | o | o | o | | | |
    |FilterModule | o | o | o | | | |
    |MapperModule | o | | o | o | o | o |

    Activation.csとDataField.csがアタッチされていない場合、テンプレートクラスはそれらを自動的にゲームオブジェクトにアタッチします。 
    シーン上で可視化結果をレンダリングするため、MeshFilterとMeshRendererをマッピングモジュールにアタッチする必要があります。
    また、マテリアルやシェーダについても適切に設定する必要があります。

 4) ゲームオブジェクトのタグ名を "VisModule" に変更します。
 5) ゲームオブジェクトをプレハブ化します。

## サンプルモジュール

|モジュール名|機能 |基底クラス |
|---|---|---|
|ReadField |テキストファイルの読み込み |ReadModuleTemplate |
|ReadV5 |VFIVE用データの読み込み |ReadModuleTemplate |
|ReadGrADS |GrADS用データの読み込み |ReadModuleTemplate |
|ExtractScalar |入力データからの単一成分の抽出 |FilterModuleTemplate |
|ExtractVector |入力データからの1～3成分の抽出 |FilterModuleTemplate |
|Interpolator |内挿補間によるダウンサイズ |FilterModuleTemplate |
|Bounds |データの境界線の描画 |MapperModuleTemplate |
|Slicer |断面図の描画 |MapperModuleTemplate |
|Isosurface |等値面の描画 |MapperModuleTemplate |
|Arrows |ベクトル場を矢印で表示 |MapperModuleTemplate |
|UIManager |ユーザインタフェース | |
|Animator |時間発展データのコントロール | |

## サンプルモジュールのテストに用いたデータセット

- テキストファイル: 本パッケージに同梱 (sample3D3.txt)
- VFIVE用データ: https://www.jamstec.go.jp/ceist/aeird/avcrg/vfive.ja.html より取得 (sample_little.tar.gz)
- GrADS用データ: http://cola.gmu.edu/grads/ より取得 (example.tar.gz)


## サンプルアプリケーション

サンプルアプリケーションは Assets/VisAssets/Scenes にあります。
読み込み後、Unityエディタ上で実行してください。

- ReadFieldSample.scene
- ReadV5Sample.scene
- ReadGrADSSample.scene

## Citation

 宮地英生, 川原慎太郎, 
 ["ゲームエンジンを用いたVR可視化フレームワークの開発"](https://www.jstage.jst.go.jp/article/tjsst/12/2/12_59/_article/-char/ja/), 
 日本シミュレーション学会論文誌, Vol.12, No.2, pp59-67 (2020), doi:10.11308/tjsst.12.59
