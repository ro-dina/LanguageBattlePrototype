# Phase 12.0 — タイトル画面・起動フロー実装レポート

実施日：2026-11-06（日本時間）。対象：Unity 6000.4.8f1 / LanguageBattlePrototype。状態：実装・検証・元プロジェクトへの反映完了。

## 1. タイトル画面

独立した`Assets/Scenes/Title.unity`を追加し、ビルドで有効なSceneの先頭にしました。既存GermanyHomeの街並み背景`Assets/material/background/GermanHomr.png`と既存日本語フォントを再利用し、茶色と金色のメニューを設けました。新規画像はありません。

表示はゲーム名、学習言語Deutsch、つづきから、はじめから、設定、アカウント・引き継ぎ、保存状態、Application.versionによるバージョンです。ゲーム名はLocalizationの`title.game.name`に集約しました。サブタイトルはありません。現行Application.versionは1.0です。WebのリリースIDとは別の値です。

## 2. Sceneと起動順

新規SceneはTitleだけです。既存Scene・Prefab・meta・GUID・fileID・Inspectorイベントは変更していません。`ProjectSettings/EditorBuildSettings.asset`の先頭へTitleを追加し、既存Scene順序とSampleSceneの無効状態を維持しました。個別のScene一覧を上書きするBuild Profile assetはありません。

EditorでGermanyHomeなどを直接開いて再生する既存の作業方法も維持しています。ビルドの起動はTitleです。Editorで新しい起動フローを確認する際はTitleを開いて再生してください。

## 3. 画面遷移

- つづきから：有効データを再検証し、既存の復元・移行・回復処理を実行してGermanyHomeへ進みます。読み込み直前にファイル状態が変わっても、データなしの初期値で代用して開始しません。
- はじめから：確認画面を開き、「開始する」の後に既存の通常リセットを実行します。成功した場合だけGermanyHomeへ移動します。
- 設定：既存SettingsUIを同じSceneに重ねます。閉じるとTitleまたはGermanyHomeの元の画面へ戻ります。戻り先の固定変更はありません。
- GermanyHome：既存の設定ボタンの横に、実行時に「タイトルへ」を追加します。進行をリセットする操作は行いません。戦闘から直接タイトルへ戻す入口は追加していません。

## 4. タイトル表示時のセーブ保護

従来のBeforeSceneLoadでの自動ロード・保存を、非TitleのSceneが読み込まれた時の初期化へ移しました。sceneLoadedは各SceneのStartより前なので、既存ゲーム画面のStartはロード済み状態を利用できます。[Unityの実行順](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-update-order/execution-order)

Titleは`SaveFileStore.Inspect`で確認するだけです。同じ検証・バックアップ探索順を利用しますが、デフォルト進行の生成、旧形式の書き換え、移行バックアップ作成、復旧ファイルの保存を行いません。起動時にPlayerSaveServiceを新設しません。

| 保存状態 | タイトルの扱い |
|---|---|
| 有効なv6進行 | つづきからが有効 |
| 移行可能な旧形式・有効な回復元あり | 復元可能と案内し、つづきからを押した後に既存処理を実行 |
| 進行なし | つづきからは無効、はじめからは確認付きで有効 |
| 設定ファイルだけ | 設定を読み込み、進行なしとして表示。進行は作成しない |
| 破損し回復元もない | 破損の案内。自動初期化せず、確認付きの新規開始のみ可能 |
| 未対応version・保存世代の異常・読み取り失敗・移行失敗 | ファイルを保護し、再開と置換を停止 |

`LoadExisting`は有効データがなければnullを返し、通常ロードの初期値へのフォールバックと分離しています。進行v6、設定version1、SaveGenerationStoreの保存世代形式は変更していません。

## 5. 新規ゲーム確認

既存データの有無にかかわらず、開始前に確認を表示します。既存データがある場合は、キャラクター・素材・Coins・ステージ進行が初期化されることと、表示言語・音量設定を維持することを明記します。「キャンセル」はメモリ・ファイルを変更しません。

「開始する」は`ProgressResetService`→`PlayerStateTransactions`→既存`CommitReset`を利用します。新しい保存世代の本体とバックアップを検証した後、既存の原子的なポインター切り替えで確定します。過去世代は自動回復先になりません。失敗時はTitleに留まり、元の進行を保持して再試行できます。

NewGameDefaultsは変更せず、Nacht 1体・3,000 Coins・マスター初期装備・既存の初期進行を使用します。Anfängerへの変更はありません。

## 6. 設定画面

既存の日本語／英語、Master・BGM・SFX・Voice、試聴、Dataタブを再利用しています。Titleへ戻った直後にUI言語が反映され、Deutschは翻訳されません。初回Titleで進行をまだ読み込んでいない間は、進行に属するチュートリアル設定とリセット操作を無効にし、「冒険を開始・再開してから」の案内を表示します。通常の設定変更はゲーム開始前にも保存できます。

GermanyHomeからの設定操作・通常リセット・部分リセットの既存仕様を維持しています。

## 7. 学習言語

`Assets/Resources/Data/learning_languages.json`で一覧を定義し、`LearningLanguageCatalog`で選択可能性を確認します。現在の一覧はDeutschだけで、未知の言語IDは拒否します。UI言語は独立したUserSettingsのja/enを使い、今回の学習言語選択はセーブ形式へ追加しません。

将来の言語追加時には一覧だけでなく、言語固有のゲームデータ・開始先・再開ルールを実装してから対応アダプターを追加してください。複数セーブスロットや先回りした移行処理は追加していません。

## 8. アカウント・引き継ぎ

入口ボタンと日英の「今後実装予定／future update」案内だけです。ログイン・クラウド保存・引き継ぎコード・QR・サーバーAPIはありません。将来はTitleScreenControllerの案内処理から専用のフローへ接続できます。

## 9. Localization

新規のタイトル用キー20件を追加しました。従来335件を変更していません。合計355件の日英文・パラメーター・参照検査が成功しています。ゲーム名、学習言語欄、保存状態、確認、未実装案内、設定の進行未読込案内、タイトル復帰、バージョン表示に対応しました。

既知のチュートリアル英語残り・副詞装備UI・共起順位相性は今回変更していません。

## 10. 追加・変更ファイル

- `Assets/Editor/WebPersistentDataBuild.cs`（変更）
- `Assets/Resources/Data/learning_languages.json`（新規）
- `Assets/Resources/Data/learning_languages.json.meta`（新規）
- `Assets/Resources/Data/ui_translations.json`（変更）
- `Assets/Scenes/Title.unity`（新規）
- `Assets/Scenes/Title.unity.meta`（新規）
- `Assets/Scripts/GameStartService.cs`（新規）
- `Assets/Scripts/GameStartService.cs.meta`（新規）
- `Assets/Scripts/GermanyHomeManager.cs`（変更）
- `Assets/Scripts/PlayerSaveService.cs`（変更）
- `Assets/Scripts/SaveFileStore.cs`（変更）
- `Assets/Scripts/SettingsInstaller.cs`（変更）
- `Assets/Scripts/SettingsUI.cs`（変更）
- `Assets/Scripts/TitleScreenController.cs`（新規）
- `Assets/Scripts/TitleScreenController.cs.meta`（新規）
- `Assets/Tests/EditMode/TitleSaveTests.cs`（新規）
- `Assets/Tests/EditMode/TitleSaveTests.cs.meta`（新規）
- `Assets/Tests/PlayMode/LanguageBattle.PlayModeTests.asmdef`（変更）
- `Assets/Tests/PlayMode/TitleSceneTests.cs`（新規）
- `Assets/Tests/PlayMode/TitleSceneTests.cs.meta`（新規）
- `Documentation/Phase12.0_タイトル画面と起動フロー実装レポート.md`（新規）
- `ProjectSettings/EditorBuildSettings.asset`（変更）
- `Tools/BuildTitleUI.cs`（新規）
- `Tools/TitleUIBatchValidation.cs`（新規）
- `Tools/TitleUIValidation.cs`（新規）

主な新規クラスは`TitleScreenController`と`GameStartService`です。同じGameStartService.cs内に`LearningLanguageDefinition`、`LearningLanguageTable`、`LearningLanguageCatalog`、`ProgressInspection`を置いています。既存Sceneは保存し直していません。

## 11. テスト

| 検査 | 結果 |
|---|---:|
| EditMode（既存206＋新規11） | 217 / 217成功 |
| PlayMode（既存86＋新規11） | 97 / 97成功 |
| 公開境界テスト | 8 / 8成功 |
| Scene / Prefab参照検査 | 26 / 26成功 |
| 日英Localization | 355 / 355成功 |
| Release / Development / Editorの通貨・リセット制限 | 既存の全検査成功 |

新規テストは保存の読み取り確認、既存v6の全セクション保持、旧形式の読み取りのみの確認、バックアップ回復、破損・未対応version・世代異常・読み取り失敗、設定のみ、初回開始、確認キャンセル、既存データ再開、新規開始と再起動、設定復帰、日英切り替え、Deutsch以外の拒否、未実装案内、Homeからの復帰、書き込み失敗と再試行、合成タッチ入力を確認します。

最初のPlayMode実行では、テストが既存の空報酬表現・リセット失敗文言を誤って期待した2件を修正しました。タッチ試験では、テスト用asmdefのInput System参照と、Editorの入力先をテスト中だけ固定する設定を追加しました。実ゲームの入力ルールは変更していません。

## 12. UI検証

日本語・英語で1920×1080、1280×720、1600×1200、2560×1080を検証しました。Title・確認・未実装案内・再開可能表示・破損・未対応versionについては390×844、844×390、800×600も確認しました。**106枚の描画、1,478件の検査が成功、失敗0**です。画面内の文字の収まり、押下位置、キャンセル、設定からの復帰、Homeとの往復も確認しました。最初の描画検査でロゴの高さ不足を検出し、文字サイズを調整して全サイズを再検査しました。

狭い画面ではTitleだけ幅基準のレイアウトにし、高さが不足する場合はメニュー・確認ダイアログをスクロールできます。対象の狭幅検証ではTitleボタンの44px以上の高さ、横方向のはみ出しがないこと、スクロール後の押下位置を検査しました。画面内に収まるときは中央に配置します。既存ゲーム全画面・Settings全体をスマートフォン向けに作り直してはいません。

## 13. Webビルド・ブラウザ

リリース用Webビルドを作成し、既存のVercelパッケージ生成・公開境界検査に成功しました。候補は12ファイル、58,156,204 bytes（約58.2 MB）、リリースIDは`ee11b467e9e95f85332dfcfa`です。ローカルHTTP検査で全12ファイルの受信バイト・ハッシュ・MIME・Brotli・キャッシュが一致し、8種類の非公開パスが404でした。

ローカルChromiumの異なる2オリジンを使い、保存領域を11時点で記録しました。

| 操作 | 確認結果 |
|---|---|
| 旧Web版→同じローカルURLでタイトル版へ更新 | 既存の進行・設定4ファイルの内容と更新日時が一致。つづきからが有効 |
| 新規ゲーム確認をキャンセル | 進行と設定の内容・更新日時が一致 |
| つづきから | 元のNachtの個体ID、2,900 Coins、獲得したsuffix_ungを保持してHomeへ |
| Home→Title | ファイルの内容・更新日時を変更しない |
| 確認後の新規開始→再読み込み | 新保存世代と新しいNacht個体、3,000 Coinsを維持。古いrootセーブは残るが復活しない |
| 新規開始時の設定 | 旧版で保存した英語UI・マスター49%を保持 |
| 別オリジンで完全初回起動 | Title表示後も保存ファイル0。つづきからが無効 |
| 初回Title→設定→英語→閉じる→再読み込み | 設定ファイルだけが存在し、進行を作成しない。英語表示とDeutsch選択を保持 |
| 狭幅390×844・844×390 | ブラウザCanvasが実サイズに追従。引き継ぎ案内を開閉でき、横画面ではメニューと確認をスクロールして新規開始できる |
| Console | 両検証セッションともerror 0件 |

ブラウザ操作はローカル候補の描画と保存内容で確認しました。観測用パネルはlocalhostの応答にだけ追加しており、リリース成果物には含めていません。実機のタッチ入力は未検証です。自動テストでは合成Touchscreenイベントを既存Input SystemのUIモジュールへ送り、選択・キャンセル・開始を確認しました。


既存`WebPersistentDataBuild`に、ブラウザ幅へ追従するCSSとviewportの指定を追加しました。Defaultのローダー構造・相対Build URL・`autoSyncPersistentDataPath = true`は維持しています。CanvasのCSSサイズへエンジンが追従する既存動作を利用しています。[Unity Webテンプレート設定](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/templates/web-templates-build-configuration)

productName・companyName・保存先・Brotli・WebAssembly設定は維持しています。ブラウザタブの名前は同じゲーム名キーを使います。ゲーム本体4ファイルのSHA-256名、MIME、Brotliヘッダー、immutableキャッシュ、HTML再検証、公開対象の検査も既存ツールを利用しています。

**Vercel Productionへのデプロイは行っていません。** 固定公開URL、公開済みファイル、公開版のブラウザ進行・設定には操作していません。候補パッケージは検証フォルダーにのみ生成し、元プロジェクトのDeploy/Vercel/publicは変更していません。

## 14. 既存データの保護

開始時はmain、`0edf0fb416b676a6d4c1a71b9447ab437b9661d7`、未コミット差分なしでした。開始時の825ファイルを記録し、反映対象以外のファイルを内容ハッシュで照合しました。実進行132ファイルと実設定2ファイルのSHA-256・更新日時・作成日時は、開始前と検証後で一致しています。反映直前と反映後の照合も一致しました。テストには作業用コピーと専用保存先のみを使用しました。

戦闘・ダメージ・副詞競合・形容詞・müssen・Futur I・TutorialFeaturePolicy・Stage1〜5・敵AI・装備・個体・合成・ガチャ・経済・素材ドロップ・共起・ドイツ語文章生成・報酬マスターとセーブ形式は変更していません。既存Scene・Prefab・metaも開始時のハッシュを維持しています。検証・WebビルドでUnityが更新したフォント、描画設定、PlayerSettingsの自動再保存差分は元に戻し、反映対象から除外しました。

## 15. 手動確認と残る課題

1. EditorでTitleを開いて再生し、既存進行があると「つづきから」が有効になることを確認する。
2. Title→設定で日本語／英語と音量を変え、閉じるとTitleへ戻り、Deutschはそのままであることを確認する。
3. 新規開始は検証用プロフィールで「はじめから」→キャンセルと、開始後のNacht・3,000 Coinsを確認する。
4. GermanyHomeの「タイトルへ」→つづきからで、個体・装備・所持品・ステージ・チュートリアル・報酬が残ることを確認する。
5. 配信前に、使用するSafari・iPhone／Android実機で縦横表示、タッチスクロール、音声の聴感、再読み込みを確認する。

Safari・実機iPhone／Androidの動作は未検証です。タッチはInput Systemへ合成イベントを送り、実際のUI入力処理を通した検査です。ブラウザの画面幅・保存保持はローカルChromiumで確認しました。既存の非同期IndexedDB自動同期を維持しており、書き込み直後の強制終了・保存拒否・容量不足に対する新しい保証は追加していません。既知の一部画像・音声不足、WebのFSR警告は今回の修正対象外です。

検証ログ・結果・スクリーンショット・候補Web成果物・反映前バックアップは`/Users/isao/Documents/ChatGPT/ゲーム/phase120-validation`にあります。
