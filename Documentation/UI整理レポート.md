# UI整理レポート

## 対象と維持したもの

ゲーム用10 SceneとUI Prefab 4点を調査・修正。SampleSceneにはUIがなく、Recovery・Unity標準サンプルは変更していません。
既存GameObjectの名前・fileID、SerializeField名、Prefab GUID、InspectorのButtonイベントを維持しています。
画像素材、ゲームデータ、レシピ、ガチャ確率、ダメージ計算、セーブ状態の形式は変更していません。

作業開始前から差分のあった `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` は今回の編集対象外です。

## 変更ファイル

| Scene | 主な変更 |
|---|---|
| GermanyHome | 左メニューのサイズと間隔を統一。ステージ選択を主要操作として配置 |
| StageSelect | 画面外にあったStage 1を画面内に移動、操作領域を拡大 |
| CharacterSelect | 一覧・ステータス・画像を分離。見出しと下部操作欄を整理 |
| CharacterCollection | 検索・フィルタ・一覧・決定操作を分離。Gridの余白とカード間隔を統一 |
| CharacterDetail | ステータス・装備・技・画像を分離。装備一覧と保存確認パネルを整備 |
| Battle | HP・ログ・副詞・技コマンドの領域を分離。副詞と技にVertical Layout Group |
| Result | 結果見出しを拡大、下部の操作サイズと間隔を統一 |
| Gacha | 1920×1080対応Canvas Scaler、10連結果用の表示領域、操作領域を拡大 |
| Inventory | 一覧と見出しを整理。Contentに縦レイアウトと高さ自動計算 |
| Crafting | 素材一覧・選択素材・合成単語・結果・操作欄を分離 |

Sceneはすべて `Assets/Scenes/` 内です。

- `Assets/Prefabs/CharacterCardButton.prefab`: 画像と名前の重なりを解消、行高176。Horizontal Layout Groupで画像がないカードは名前欄が広がります。
- `Assets/Prefabs/EquipmentButtonTemplate.prefab`: 行高72、親の幅に追従。
- `Assets/Prefabs/InventoryItemPrefab.prefab`: 行高80、名前と個数を行内に配置。
- `Assets/Prefabs/CraftingMaterialPrefab.prefab`: 同上。重複する仮ラベルを非表示。
- `Assets/Scripts/CharacterCollectionManager.cs`: DB参照・フィルタ範囲・nullデータのチェック、一覧再生成時の即時非表示、購読解除。
- `Assets/Scripts/CharacterSelectManager.cs`: DB参照チェック、一覧再生成時の即時非表示。
- `Assets/Scripts/CharacterDetailManager.cs`: 確定ボタンのイベント競合を修正、null/範囲チェック、再生成時の即時非表示。
- `Assets/Scripts/ResultManager.cs`: 結果テキストのnullチェック。

全Canvasを1920×1080基準のScale With Screen Size / Expandに統一。異なる比率では表示領域を広げ、基準レイアウトが収まる設定です。主要操作は概ね高さ72〜96、標準余白は48、一覧内余白は16、行間は12〜16。画面の区画はAnchor、動的一覧はUnity標準Layout GroupとContent Size Fitter、一覧行はLayout Elementで管理します。文字には内側余白・自動サイズ調整・省略表示を設定しています。

## 修正した不具合

1. **Inventoryの生成先がScroll View本体**: 更新時にViewportとScrollbarも破棄されていました。参照をViewport/Contentに修正。
2. **Craftingの生成先もScrollView本体**: 素材がContent外に生成されていました。同様に修正。
3. **技装備パネルが3D Plane**: Transform・MeshRenderer・MeshColliderを持ち、子UIに約0.51の縮尺が設定されていました。同じGameObject/Transform IDでRectTransform化し、不要な3Dコンポーネントを除去。
4. **技装備一覧にLayout Groupがない**: 生成したボタンが重なっていました。縦レイアウトと高さ自動計算を追加。
5. **確定ボタンにStartBattleが残る**: `RemoveAllListeners`ではInspector由来のイベントが除去されません。用途を切り替える際にButtonClickedEventを作り直し、Confirm/Battleのいずれか一つを登録。
6. **装備モーダルより後ろの兄弟UIが手前に出る**: モーダルの描画順を整理。
7. **一覧再生成のDestroyがフレーム末まで遅延**: 旧行を直ちに非表示にし、同一フレームの重複表示とクリックを防止。
8. **詳細Scene単独起動時のDB不足**: 他のキャラクターSceneと同様にGameDatabaseを配置。通常遷移時は既存Singleton処理を利用。
9. **画面外・枠外UI**: Stage 1、キャラクター選択見出し、詳細画像、極小のScrollView、Prefabの名前/個数位置を修正。

## 仕様判断が必要なため維持したもの

- Missions / Dictionary / Settingsは既存の「coming soon」ログのみです。
- `GermanyHomeManager.BackToEuropeMap()`は存在しないEuropeMapを指定していますが、現在のSceneにはこのメソッドのButton登録がありません。
- 所有キャラクターの制限、キャラクター間で共有される装備選択状態、所持品の永続保存などのゲーム仕様は維持しています。
- 戦闘の音声リソース不足による警告があります。既存の単語音声へのフォールバック処理を維持しています。
- 図鑑には画像素材のないキャラクターもあります。既存の非表示処理を維持し、名前欄が余白を利用できるようにしました。
- 非常に長い選択素材・装備名は省略表示される場合があります。全文閲覧機能の追加は行っていません。

## 検証結果

- Unity **6000.4.8f1**でゲーム本体と検証ランナーのコンパイル成功。
- 既存GameObject名/fileID、Inspectorに保存されたButtonイベントが保持されていることを変更前ファイルとの比較で確認。
- 静的検査で15ファイル（UIなしのSampleSceneを含む11 Sceneと4 Prefab）の参照・Script・Manager必須参照・イベントメソッドを確認。検出エラー0。
- 作業用コピーでPlay Modeを実行。操作・矩形チェック **4,198件成功、失敗0件**。
- 一覧の繰り返し更新、検索、素材クリアと合成、装備一覧の繰り返し開閉・選択、未保存確認とCancel、図鑑からのConfirm、選択からのBattle、技パネルの開閉、戦闘1ターン、詳細Sceneの単独起動を確認。
- 主要操作14回でGraphicRaycasterによる到達先とクリックイベントを確認。全ボタンの全組み合わせを検証したものではありません。
- 10画面と4種の開閉状態を、**1920×1080 / 1280×720 / 1600×1200 / 2560×1080**で描画（56画像）。固定操作ボタンの画面内収まり、一覧行のサイズと重なり、ScrollRectの親子関係を検査。検証データのTMP文字あふれ警告は0。
- ゲーム側の残存警告: `Audio/Sentences/jahr_ist_feuer` がなく単語音声へフォールバック。
- 検証Editorでは `UnityEditor.Search.SearchDatabase` の `ArgumentOutOfRangeException` が発生。スタックはUnity内部の検索インデックス初期化で、ゲーム側のUI検査とは分けて記録しています。Editor全体のConsoleエラーが0とはしていません。

検証画像・生ログ: /Users/isao/.codex/visualizations/2026/09/27/01a0e14e-c8ef-77b1-91a2-2d7e9fcea4c9/ui-review

## 検証方法

`python3 Tools/ui_audit.py` はゲームSceneとPrefabのローカル参照、アセットGUID、Missing Script、Managerの未割当フィールド、Inspectorのメソッド名を検査します。`--dump`でHierarchy、RectTransform、Anchor/Pivot、Layout設定、Managerの参照を出力できます。

Unity用の `Tools/UIBatchValidation.cs` / `Tools/UIPlayValidation.cs` は**作業用コピー専用**です。前者をコピー先のAssets/Editor、後者をAssetsへ置き、プロジェクト指定版Unityから `-batchmode -projectPath <作業用コピー> -executeMethod UIBatchValidation.Run -logFile <ログ>` を実行します。検証用に所持品・装備・Sceneを操作するため、普段の作業プロジェクトには配置しないでください。

描画検証は検証用Canvasを一時的にScreen Space Cameraへ切り替えてRenderTextureに出力します。通常のGame ViewはScreen Space Overlayです。マウス実操作・実機の最終確認は以下の手順で行ってください。

## Editorでの最終確認

- 開いたSceneに未保存の変更がある場合は、ディスク上の今回の修正を上書きしないようにしてSceneを再読み込み。
- 1920×1080、1280×720、1600×1200、2560×1080のGame Viewで文字・画像・余白を確認。
- 所持品が0件/多数の場合と、長いアイテム名でスクロールの先頭・末尾を確認。
- 図鑑→詳細→確定、選択→詳細→バトル、装備一覧の開閉と選択を確認。
- 未保存の装備変更がある状態で戻る→Save / Discard / Cancelをそれぞれ確認。
- 素材選択→Clear→再選択→Craft、10連ガチャ、戦闘の副詞選択→Fight→技→次ターンを確認。
- Consoleに新しいUIエラーがないことを確認。音声不足など既存の警告は上記のとおり別扱いです。
