# Phase 10.1：戦闘勝利報酬・文字列ドロップ・動的素材

2026-10-31 / Unity 6000.4.8f1

勝利時の100 Coins、敵名の各文字40%抽選、連続成功部分の素材化、全成功時の敵キャラクター個体獲得を実装しました。動的素材は既存インベントリ・自由合成・セーブへ接続しています。実装・テスト・画面検証は作業用コピーと専用セーブ先で行い、実プレイヤーのセーブは検証入力に使用していません。

## 1. 変更・追加ファイル

- `Assets/Resources/Data/battle_rewards.json`（新規）
- `Assets/Resources/UI/BattleRewardRetryUI.prefab`（新規）
- `Assets/Scenes/Result.unity`
- `Assets/Scripts/BattleManager.cs`
- `Assets/Scripts/BattleRewardRetryUI.cs`（新規）
- `Assets/Scripts/BattleRewards.cs`（新規）
- `Assets/Scripts/CharacterDictionary.cs`
- `Assets/Scripts/CraftingUIManager.cs`
- `Assets/Scripts/DynamicMaterials.cs`（新規）
- `Assets/Scripts/InventoryManager.cs`
- `Assets/Scripts/InventoryUIManager.cs`
- `Assets/Scripts/ItemDisplayNames.cs`
- `Assets/Scripts/MaterialRowLayout.cs`（新規）
- `Assets/Scripts/PlayerSaveService.cs`
- `Assets/Scripts/PlayerStateTransactions.cs`
- `Assets/Scripts/ResultManager.cs`
- `Assets/Scripts/RewardGrantService.cs`
- `Assets/Scripts/SaveData.cs`
- `Assets/Scripts/SaveDataMigration.cs`
- `Assets/Scripts/SaveFileStore.cs`
- `Assets/Tests/EditMode/BattleRewardTests.cs`（新規）
- `Assets/Tests/EditMode/GachaContentTests.cs`
- `Assets/Tests/EditMode/SaveMigrationTests.cs`
- `Assets/Tests/EditMode/SaveTests.cs`
- `Assets/Tests/EditMode/WordItemMigrationTests.cs`
- `Assets/Tests/PlayMode/BattleRewardSceneTests.cs`（新規）
- `Documentation/Phase10.1_戦闘勝利報酬・動的素材実装レポート.md`（新規）
- `Tools/BuildBattleRewardUI.cs`（新規）
- `Tools/UIPlayValidation.cs`

新規meta 8件を含め計37ファイルです。既存meta・Prefab・背景・画像は変更していません。既存Sceneの変更はResultのみです。

主な新規型はDynamicMaterials、BattleRewardSettings、BattleRewardRecord、BattleRewardState、BattleDropPlan、BattleDropGenerator、BattleRewardService、BattleRewardRetryUI、MaterialRowLayoutです。新規テストはBattleRewardTests、BattleRewardSceneTestsです。

## 2. 勝利から報酬確定まで

BattleManagerの既存CompleteBattleが、通常攻撃・未来形の予約攻撃・敵攻撃後の勝敗を判定する唯一の接続箇所です。既存のダメージ・文章・音声・演出処理は維持しました。

戦闘開始時にBeginで一意の戦闘IDと敵の種類IDを保存します。IDの保存に失敗した場合は入力を止め、再試行画面を表示します。勝利確定後にCompleteで報酬を準備し、通貨・素材または新個体・確定記録を同時保存します。保存成功後にResultへ進みます。敗北は報酬を付けず、敗北記録のみ保存します。

勝利報酬の保存に失敗してもResultへ進みません。プレイヤー状態を維持し、同じ抽選結果で「Retry save」を行えます。再試行中に勝敗を変更する要求も拒否します。詳細な保存エラーはConsoleへ記録し、ゲーム画面には操作に必要な案内だけを表示します。

調査上、StageSelectManagerというクラスはなく、SceneLoader.LoadStage1がFeuerを選んでCharacterSelectへ移動します。StageProgressは保存・復元用で、既存の勝利処理からクリア記録を追加する実処理はありませんでした。今回もステージ解放・初回報酬・進行ルールは追加していません。

## 3. 抽選方法

Assets/Resources/Data/battle_rewards.jsonにvictoryCoins=100、letterDropChance=0.4を設定しました。既存economy_settings.jsonの初期3,000・ガチャ100/1,000・合成200は変更していません。

敵名をNFC正規化してStringInfoのテキスト要素単位で走査します。ä/ö/ü/ßは1要素、分解表記のウムラウトも合成して判定します。各要素について独立した一様乱数を1回取得し、値が確率未満なら成功です。確率はdoubleで扱い、0.40ちょうどは40%設定で失敗となります。

通常はUnity Random.Rangeの0〜999,999を1,000,000で割った[0,1)の値を使います。BattleDropGenerator.Rollには抽選関数と確率を注入でき、固定結果による再現テストや将来の敵・ステージ別設定に利用できます。既存戦闘の行動抽選順は変更していません。

## 4. 連続文字列の結合

成功した文字を作業中の文字列へ追加し、失敗位置で確定・区切ります。走査終了時にも残りを確定します。失敗をまたいだ連結は行いません。

Feuerで○○○×○ならFeuとr、○○×○○ならFeとerです。同じ文字列が別の成功区間から発生した場合は数量を合算します。全失敗なら素材は0件ですが、勝利Coinsは付与します。

## 5. 全成功時の直接獲得

全要素が成功した場合は素材リストを空にして、戦闘開始時に記録した敵の種類IDを報酬とします。名前から別キャラクターを推測しません。新しいcharacter-GUID個体を1体作り、発見記録を更新します。既存個体・その装備は保持し、新個体はマスター初期装備を使います。

全成功以外の部分文字列は、Wasserなど別のキャラクター名と一致しても動的素材として保持します。ガチャのwordアイテム変換とは別に、明示した種類IDを個体報酬へ渡します。

## 6. 動的素材の識別・保存

InventoryEntryに動的素材用の表示textを追加しました。固定itemIdと共存し、動的素材のIDは次の形式です。

material-text-v1: + Base64URL(UTF-8(NFC・不変小文字化した文字列))

同じ正規化文字列は常に同じIDです。大小文字・NFC/NFDをまとめ、äとae、ßとssは区別します。最初に取得した自然な綴りをtextへ保持し、後続の同じ素材は数量だけ増やします。固定letter_eと動的eは別の材料です。

受理する文字列は文字・結合記号からなり、先頭は文字、最大128 UTF-16コード単位・512 UTF-8バイトです。空白、制御文字、タグ、壊れたUnicode、空文字、長すぎる文字列、非正規のID、IDと表示textの不一致を拒否します。数量は正のintで、加算時にオーバーフローを拒否します。セーブ全体の4 MB上限も維持しています。

InventoryManager.AddTextで安全に付与できます。固定素材と動的素材は同じ一覧に表示します。MaterialRowLayoutが長い動的素材名を折り返して行の高さを増やすため、名前と数量を分離した既存レイアウトを維持できます。

## 7. 自由合成との接続

WordCharacterRewards.TextとItemDisplayNamesが動的素材のtextを参照します。既存CraftingElementのMaterialをそのまま使い、選択順・可変個数・同じ素材の複数選択・Undo/Clear・確認ダイアログを維持しました。

Feu + er → Feuer、動的Feu + 固定chunk_er、Wasser個体 + 動的pistole、および動的eを2回使うFeuer合成を検証しています。成功時は200 Coinsと指定した素材数量・個体だけを消費し、新個体を作ります。所有数不足・不正な組合せ・保存失敗・Cancelで失いません。

## 8. 一括確定

RewardGrantService.ApplyToを、通貨・固定/動的素材・明示したキャラクター種類IDをまとめて扱う形へ拡張しました。BattleRewardServiceは既存PlayerStateTransactionsで候補状態を作り、報酬と受取済み記録を同じ候補へ適用します。

全体検証と原子的保存が成功した後に、所有・通貨・素材・記録をメモリへ反映し、PlayerStateChangesとBalanceChangedを通知します。保存失敗時に一部だけ付与しません。既存ガチャの確率・料金・報酬経路も維持しています。

## 9. 二重付与防止

保存するBattleRewardStateは「最新1戦闘のチケットと結果」です。戦闘IDはbattle- + GUIDで、開始時に永続化します。Completeは保存された最新IDと一致し、Pendingである場合だけ付与できます。同じ確定済みIDの再通知は成功済みとして返し、再加算しません。異なる結果への変更は拒否します。

新しい戦闘を開始すると新IDへ入れ替えます。過去IDの受取済み一覧を削除して再び未受取扱いにする方式ではなく、未知・過去IDは常に拒否します。このため保存記録は無制限に増えず、再起動後も古いIDを受け取れません。Resultの表示処理には報酬付与APIの呼出しがありません。

この設計は既存ゲームと同じく同時に1戦闘を扱います。次の戦闘開始後、Resultの履歴表示は前の戦闘ではなく最新記録を参照します。

## 10. version 5と移行

version 5は既存v4の全セクションを維持し、動的InventoryEntry.textとbattleRewardsを追加します。v1〜v4からv5へ移行できます。v4のCoinsは0を含めそのまま維持し、v1〜v3の初期通貨付与・決定的個体ID・旧単語アイテム変換・convertedWordItemsを維持しました。

旧ソースを.v1.bak〜.v4.bakへ保護してから置換します。競合する保護バックアップ、通常バックアップ、破損本体、recoveryファイルの扱いは既存処理を維持します。移行失敗時は元ファイルを保持し、未対応のv6以降では保存を停止します。v4の不正な残高は0へ読み替えません。

新規ゲームと移行直後は戦闘記録なしです。Unity JsonUtilityがnullの入れ子を空オブジェクトとして往復する場合も、未発行状態として明示的に検証します。IDなどが欠けた不正な受取記録を有効な戦闘として使用しません。

実プレイヤーの移行は反映後の次回Play時です。今回の検証で実セーブを移行・初期化していません。

## 11. Resultの変更

既存Result Sceneにスクロール可能な報酬領域を追加し、見出しを上部へ配置しました。勝敗背景・Stage Select/Homeボタン・Inspectorイベント・旧fileIDは維持しています。

保存済み勝利記録から敵名、Coins、素材と数量、直接獲得したキャラクターを表示します。0件のセクションは出しません。敗北では勝利報酬を非表示にします。長い名前は折り返し、多数の報酬はスクロールでき、下部操作と重なりません。保存済み結果は再表示・再起動後も閲覧でき、表示だけでは付与されません。

## 12. Edit Mode / Play Mode

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode | 144 | 0 |
| Play Mode | 45 | 0 |
| 合計 | 189 | 0 |

既存の全テストを含みます。新規テストでは独立抽選・40%境界・連続区間・全失敗/全成功・Unicode・安定ID・不正ID・大小文字スタック・長さ上限・動的/固定/個体の混在合成・数量超過・料金200を検証しました。

勝利100、敗北0、通貨と素材/個体の同時確定、保存失敗時の全状態不変、抽選結果を保持した再試行、勝敗変更拒否、再起動と過去ID再送、Result再表示、長い報酬のスクロール、v4移行・保護復旧・0残高保持も確認しています。既存の通常攻撃・Futur I・müssen・副詞競合・装備・文章・ガチャ・移行の回帰テストを維持しました。

証跡：[Edit Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/edit-final.xml)、[Play Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/play-verified.xml)。

## 13. UI検証

1920×1080、1280×720、1600×1200、2560×1080で184枚を実描画しました。12,702項目成功、失敗0件です。

対象にはBattle、Result、Inventory、Crafting、CharacterSelect、CharacterCollectionを含みます。部分ドロップ・全成功の個体獲得・敗北・多数の長い報酬・動的素材の一覧と折り返し・合成確認・保存失敗再試行を描画し、スクロール、画面内配置、旧ボタンへのポインター到達を検査しました。長い素材行は実際にスクロール表示した画像も保存しました。

17 Scene/PrefabのGUID・ローカル/アセット参照・既存Inspectorイベントを検査しました。既存metaの変更はありません。Result以外の既存Scene/Prefabの内容は維持しています。

証跡：[UI結果](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/ui-verified/validation.txt)、[部分ドロップ結果](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/ui-verified/Result-material-rewards-1280x720.png)、[長い素材一覧](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/ui-verified/Crafting-long-material-row-1280x720.png)、[保存再試行](/Users/isao/Documents/ChatGPT/ゲーム/phase101-validation/ui-verified/Battle-reward-save-retry-1280x720.png)。

## 14. 残っている問題・範囲

今回の実装に未解決のテスト失敗はありません。UI検証の警告は、既存の不足音声8種類と、保存失敗の故意再現に伴う診断1種類です。長い素材名の省略警告とUnity内部SearchDatabase診断は0件です。音声素材の追加は行わず、既存フォールバックを維持しています。

進行中の戦闘そのものを再起動後に再開する機能は追加していません。保存失敗中に終了すると未確定報酬は付与されず、次の新戦闘で最新チケットを置換します。保存済み報酬の二重受取は防ぎます。過去セーブへの手動巻戻しに対するサーバー照合等は今回のローカルゲームの対象外です。

Phase 10.2で初回クリア・通常クリア・解放を接続する場合は、BattleRewardService.Completeの同じ候補状態でStageProgressと追加報酬を更新してください。報酬保存後の別処理としてクリア記録を付けると不整合が起きるため、分離しないことが必要です。

今回はチュートリアル・ミッション・分解・強化・SPD順変更・ステージ初回報酬の実処理を追加していません。アプリ全体の製品ビルド・実機配布は未実施です。

## 15. Editorでの手動確認

1. 再インポート完了後にPlayする。v4セーブは保護バックアップを作ってv5へ移行する。残高・所有個体・装備が維持されることを見る。セーブの削除・初期化は不要。
2. Stage 1から戦闘し勝利する。ResultにCoins +100と抽選に応じた素材、または全成功時のFeuer個体が表示されることを見る。全失敗ならCoinsだけとなる。
3. 敗北では勝利報酬が出ないことを見る。Stage Select/Homeの操作と背景が維持されることを確認する。
4. Inventoryで獲得素材を確認し、Craftingで固定素材・動的素材・個体を順に組み合わせる。候補が辞書に一致した場合は200 Coinsの確認を経て合成する。Cancelで減らないことを見る。
5. Resultへ戻る・再表示する・Playを再起動しても同じ報酬が再加算されないことを見る。別の新戦闘では新たな勝利報酬を獲得できる。
6. 残高と素材の更新、長い名前の折り返し、報酬のスクロールを4解像度で確認する。保存失敗の故意再現は実プロフィールでは行わず、専用テストを使う。

元EditorのPlay停止をユーザー確認し、反映対象を検証済みファイルだけに限定しました。変更前ソースを退避し、既存差分・旧GUID/fileID・Inspectorイベント・マスター・確率・コスト・画像を照合しました。実プロフィール93ファイルの内容ハッシュ・作成日時・更新日時は作業前後で一致しています。
