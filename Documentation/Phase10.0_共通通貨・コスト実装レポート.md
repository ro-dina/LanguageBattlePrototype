# Phase 10.0：共通通貨・ガチャ／合成コスト・通貨UI

2026-10-31 / Unity 6000.4.8f1

共通通貨Coin（表示名Coins）を導入しました。初期3,000、単発ガチャ100、10連1,000、合成200を設定データから読み込みます。減算と報酬付与・素材消費は既存の準備済み状態を保存してから反映する仕組みに統合しています。既存の未コミット変更を保護し、実プレイヤーのセーブは検証に流用・変更していません。

## 1. 変更・追加ファイル

- `Assets/Resources/Data/economy_settings.json`（新規）
- `Assets/Resources/UI/CurrencyHUD.prefab`（新規）
- `Assets/Scenes/Gacha.unity`
- `Assets/Scripts/CraftingManager.cs`
- `Assets/Scripts/CraftingUIManager.cs`
- `Assets/Scripts/CurrencyHUD.cs`（新規）
- `Assets/Scripts/CurrencyHUDInstaller.cs`（新規）
- `Assets/Scripts/CurrencyService.cs`（新規）
- `Assets/Scripts/DevelopmentCurrencyAccess.cs`（新規）
- `Assets/Scripts/EconomySettings.cs`（新規）
- `Assets/Scripts/GachaManager.cs`
- `Assets/Scripts/PlayerSaveService.cs`
- `Assets/Scripts/PlayerStateChanges.cs`
- `Assets/Scripts/PlayerStateTransactions.cs`
- `Assets/Scripts/RewardGrantService.cs`
- `Assets/Scripts/SaveData.cs`
- `Assets/Scripts/SaveDataMigration.cs`
- `Assets/Scripts/SaveFileStore.cs`
- `Assets/Tests/EditMode/CharacterInstanceTests.cs`
- `Assets/Tests/EditMode/CurrencyTests.cs`（新規）
- `Assets/Tests/EditMode/DictionaryCraftingTests.cs`
- `Assets/Tests/EditMode/GachaContentTests.cs`
- `Assets/Tests/EditMode/SaveMigrationTests.cs`
- `Assets/Tests/EditMode/SaveTests.cs`
- `Assets/Tests/EditMode/WordItemMigrationTests.cs`
- `Assets/Tests/PlayMode/CharacterInstanceSceneTests.cs`
- `Assets/Tests/PlayMode/CurrencySceneTests.cs`（新規）
- `Assets/Tests/PlayMode/SaveSceneTests.cs`
- `Tools/BuildCurrencyUI.cs`（新規）
- `Tools/UIPlayValidation.cs`
- `Tools/ui_audit.py`
- `Tools/verify_currency_build_flags.py`（新規）
- `Documentation/Phase10.0_共通通貨・コスト実装レポート.md`（本書、新規）

新規アセット・フォルダのmeta 10件を含め、計43ファイルです。既存meta・Prefabの定義は変更していません。Scene変更はGachaだけです。ほかの7画面では共通Prefabを実行時に配置します。

主な新規型はCurrencyService、WalletState、PlayerOperation、EconomySettings、DevelopmentCurrencyAccess、CurrencyHUD、CurrencyHUDInstallerです。新規テストはCurrencyTestsとCurrencySceneTestsです。

## 2. 通貨管理の構造

WalletState.balanceは符号付き64ビット整数longです。有効範囲は0から9,223,372,036,854,775,807までで、加算前に上限、減算前に残高を確認します。負数は拒否します。0は有効な無変更額です。

CurrencyServiceでBalance、CanSpend、TryAdd、TrySpend、BalanceChangedを提供します。AddTo/SpendFromは準備中のSaveData候補へ適用する関数で、単独では保存・公開しません。通常の増減APIは必ずPlayerStateTransactionsを通します。

PlayerOperationは1つの操作意思を表すトークンです。成功したトークンの再確定を拒否し、保存に失敗したトークンは再試行できます。確定処理中の再入も拒否します。UIには同時処理ガードと短い連打制限を併用しています。別の正規操作を行う際は新しいトークンを使います。

残高変更通知はファイル保存と全状態反映の後です。同じ残高を再ロードしても重複通知しません。通知先の例外は個別に記録して処理を継続するため、保存済み操作を失敗扱いにして再実行することを防ぎます。

## 3. version 4と移行

既存のセクション（所有個体、個体別装備、所持品、発見記録、ステージ進行、convertedWordItems）を維持し、wallet.balanceを追加してversion 4へ更新しました。

- 新規ゲームは設定のinitialCoinsを使います。
- v3からは既存データを検証・維持し、初回だけ初期通貨を追加します。
- v1/v2は既存の個体ID移行・旧単語アイテム変換を維持してv4まで移行します。
- 有効なv4は保存済み残高をそのまま使います。残高0でも再付与しません。

移行前のバイト列を.v1.bak／.v2.bak／.v3.bakへ保護し、検証・保存が成功した場合のみ本体を置換します。同じ旧ソースからの再試行は既存の決定的IDを使用し、初期通貨を加算し続ける処理にはしません。通常バックアップ、保護バックアップ、recoveryファイルからの復旧を維持しています。将来のversion 5以降は保存を停止して保護します。

v4の残高はJSONにも明示された範囲内の整数であることを検証し、wallet欠落・小数・負数・オーバーフローを0へ読み替えません。既存の4 MB上限も維持します。

実セーブ自体の移行は、反映後の次回Play／ゲーム起動時にバックアップを作成して実行します。今回、実セーブへ初期通貨を先行付与していません。

## 4. 初期通貨と費用設定

設定ファイルは`Assets/Resources/Data/economy_settings.json`です。

| 設定 | 初期値 |
|---|---:|
| initialCoins | 3,000 |
| gachaSingleCost | 100 |
| gachaTenCost | 1,000 |
| craftingCost | 200 |
| developmentGrants | 100 / 1,000 / 10,000 |

currencyDisplayNameはCoins、coinSymbolはCです。表示名・簡易アイコン表現は残高データから分離しています。表示と費用処理は同じ設定を参照し、ソース内の金額定数を別々に調整する必要はありません。設定の負数などは拒否します。設定変更は次回起動・Playから反映されます。

## 5. 共通通貨UI

新規`Assets/Resources/UI/CurrencyHUD.prefab`に通貨表示と追加ダイアログをまとめました。CurrencyHUDInstallerが主要Sceneのロード時に1つだけ配置し、遷移先ではそのSceneに属する新しい表示へ切り替えます。表示ロジックのSceneごとのコピーはありません。

対象はGermanyHome、Gacha、Crafting、Inventory、CharacterSelect、CharacterDetail、CharacterCollection、StageSelectの8画面です。Battle・Result・SampleSceneには今回追加していません。

右上に金色のC、3桁区切りの残高、＋を表示します。変更通知で即時更新し、遷移・再ロード後も現在値を使います。1920×1080基準・ExpandのCanvasScalerを使い、長い残高は文字サイズを調整して収めます。背景とキャラクター画像は変更していません。

## 6. 開発用＋

＋を押すとCurrentと+100／+1,000／+10,000、Closeを表示します。付与はCurrencyService.TryAddと正式な保存経路を使い、成功後に所持金とダイアログを更新します。即時の重複クリック、処理中の再入を拒否し、保存失敗時は残高を変えません。

UNITY_EDITORまたはDEVELOPMENT_BUILDが定義された場合だけ開発用付与を有効にします。通常Releaseでは＋を非表示にし、開発用処理の入口とDevelopmentCurrencyAccess.Runの両方で実行を拒否します。ショップ・課金処理はありません。

正規の将来報酬に必要なCurrencyService.TryAddはReleaseでも利用できます。開発用UIからその経路へ到達できない構造です。

## 7. ガチャと通貨の整合性

残高と実行状態を先に確認し、不足時は乱数を進めません。単発または10件の抽選結果を揃えた後、候補状態へ通貨減算と全報酬を適用します。検証・保存成功後に所持状態と結果画面を更新します。

10連は1つの確定処理です。途中の数件だけを付与しません。素材、直接獲得するキャラクター個体、同じ種類の重複当選も同じ経路です。固定抽選結果を使う検証用入口GrantDrawsにも同じ料金・検証・トークン制約があります。

GachaTableとgacha_items.jsonは変更していません。N60%、R25%、SR12%、SSR3%、30文字各2%を維持しています。画面には現在残高、必要額、残高不足の理由を表示し、実行不可のボタンを無効にします。

## 8. 合成と通貨の整合性

文字素材のみ、個体のみ、混在のすべてで同じcraftingCostを適用します。辞書照合・所有確認・残高確認後、共通の確認ダイアログを開きます。文字素材のみでも確認が必要です。

確認には素材、個体と装備、200 Coins、現在残高、生成結果を表示します。Cancelは無料です。確認中に残高や所持が変わると表示と確定可否を更新し、最終確定時にも再検証します。

候補状態で素材・指定個体・その装備・Coinsを消費し、初期装備の新個体を作ります。同種の別個体と図鑑の発見記録を保持します。失敗時は何も消費しません。自由な文字列連結、選択順、可変個数、Undo/Clearを維持しています。

## 9. 原子的な保存と将来の報酬

既存PlayerStateTransactionsの「状態コピー→候補変更→検証→原子的保存→メモリ一括反映」を拡張しました。通知をまとめるだけではなく、保存に失敗した候補は公開しません。通貨・素材・個体・装備の一部だけが減ることを防ぎます。

RewardGrantService.Grant(rewards, coins, out error, operation)で、登録済み素材・単語由来の新個体・通貨をまとめて付与できます。候補へ適用するApplyToも用意し、ガチャの費用と報酬を同じ確定処理へ接続しています。次のPhaseでミッションや戦闘報酬へ接続できます。

今回、勝利報酬、ドロップ、ミッション、分解、強化、動的な文字列素材、SPDによる攻撃順は追加していません。

## 10. Edit Mode / Play Mode

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode | 122 | 0 |
| Play Mode | 39 | 0 |
| 合計 | 161 | 0 |

新規・更新テストでは、初期通貨、加算・減算・0残高、負数・上限、保存復元、保存失敗時の全状態保持、同じトークンの再実行拒否、通知先の例外、混在報酬を確認しました。

ガチャは100／1,000の減算、不足時の乱数不変・報酬なし、保存失敗時の全状態不変、10件の原子的付与、重複個体を検証しています。合成は200の減算、素材・個体・通貨の同時確定、文字素材のみの確認、Cancel、確認中の残高変化、保存失敗・入力不正を検証しています。

v1/v2/v3からv4への移行、初期通貨の1回付与、保存済み0残高、個体ID・装備・進行・旧単語変換・記録保持、v3保護バックアップからの復旧と再付与防止を確認しました。既存の全戦闘・文法・装備・合成・セーブテストも実行しています。

さらに同じDevelopmentCurrencyAccess.csを、通常Release・DEVELOPMENT_BUILD・UNITY_EDITORの3条件でコンパイルして実行しました。Releaseはenabled=False／grants=0、DevelopmentとEditorはenabled=True／grants=1でした。これは開発用ゲートのコンパイル・実行検証で、アプリ全体のRelease書き出し・配布は行っていません。

証跡：[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/edit-verified.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/play-verified.xml)、[ビルド定義検証](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/build-flags/results.txt)。

## 11. UI検証

1920×1080、1280×720、1600×1200、2560×1080で132枚を実描画しました。7,628項目成功、失敗0件です。

主要8画面の所持金、遷移後の一致、＋ダイアログの各ボタン、ガチャ料金・不足、合成の200 Coins・Cancel・確認中の残高変化・確定後の0表示、long上限の長い残高を確認しました。画面内配置とポインター到達を検査し、既存の個体切替・装備・合成・戦闘UIの検証も維持しています。

16 Scene/PrefabのGUID、fileID、ローカル・アセット参照、既存Inspectorイベント検査が成功しています。Gachaの既存52ブロックを保持し、費用表示用4ブロックを追加しました。他の既存Scene/Prefabは変更せず、共通HUDは新規Prefabです。

証跡：[UI結果](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/ui/validation.txt)、[通貨追加](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/ui/Currency-add-dialog-1280x720.png)、[不足時の合成確認](/Users/isao/Documents/ChatGPT/ゲーム/phase10-validation/ui/Crafting-funds-changed-1920x1080.png)。

## 12. 残っている問題・検証の範囲

今回追加した機能の未解決テスト失敗はありません。通常Releaseの開発用付与停止はコンパイル条件別の実行で確認していますが、製品アプリ全体のReleaseビルド・実機配布は未実施です。

既存の未配置画像・音声素材は追加していません。UI検証では従来の音声不足警告が8種類あり、文章音声から単語音声へのフォールバックは維持しています。Unity内部SearchDatabase診断は0件でした。

通貨トークンは実行中の同じ操作意思を再確定しないためのものです。将来のミッション報酬では、ミッション完了記録も同じ候補状態へ含めて管理してください。今回はミッションや勝利報酬の実処理を追加していません。

## 13. Editorでの手動確認

1. 再インポート完了後、Playする。旧セーブは保護バックアップを作ってv4へ移行し、初回3,000 Coinsとなる。セーブの削除・初期化は不要。
2. 主要画面右上の残高を確認し、＋から100／1,000／10,000を追加する。遷移しても残高が一致することを見る。
3. ガチャ1回で100、10連で1,000減り、報酬がまとめて入ることを確認する。
4. 合成では文字だけの場合も200 Coinsの確認が出る。Cancelで減らず、確定後に素材・対象個体・通貨が減って新個体が増えることを見る。
5. 残高不足ではガチャ・合成が無効になり、理由が表示されることを見る。既存のFutur I・müssen・副詞操作もそのまま利用できる。
6. Playを終了して再度起動し、残高が保存され、初期3,000が再付与されないことを確認する。

検証は作業用コピーと隔離セーブ先で行いました。反映対象は旧ソースを退避して照合し、実プロフィール全ファイルの内容ハッシュ・作成日時・更新日時を作業前後で確認しました。
