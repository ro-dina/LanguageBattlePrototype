# Phase 10.2：ステージ進行・初回報酬・チュートリアル共通基盤

2026-11-01 / Unity 6000.4.8f1

ステージ1〜5の進行、初回300 Coins、従来の勝利100 Coins、スキップ設定、共通会話と操作制限を実装しました。Technikは受講から実攻撃・説明・勝利まで操作できます。残り4ステージは通常戦闘・報酬・再挑戦・3択を利用でき、詳細誘導は未実装として明示します。

## 1. 変更・追加ファイル

- `Assets/Resources/Data/characters.json`
- `Assets/Resources/Data/dialogues.json`（新規）
- `Assets/Resources/Data/stages.json`（新規）
- `Assets/Resources/Data/tutorials.json`（新規）
- `Assets/Resources/UI/DialogueUI.prefab`（新規）
- `Assets/Resources/UI/StageRow.prefab`（新規）
- `Assets/Resources/UI/TutorialChoiceUI.prefab`（新規）
- `Assets/Resources/UI/TutorialFocusUI.prefab`（新規）
- `Assets/Scenes/StageSelect.unity`
- `Assets/Scripts/BattleManager.cs`
- `Assets/Scripts/BattleModifierUI.cs`
- `Assets/Scripts/BattleRewards.cs`
- `Assets/Scripts/CharacterDetailManager.cs`
- `Assets/Scripts/CharacterSelectManager.cs`
- `Assets/Scripts/DialogueUI.cs`（新規）
- `Assets/Scripts/GermanyHomeManager.cs`
- `Assets/Scripts/PlayerSaveService.cs`
- `Assets/Scripts/PlayerStateTransactions.cs`
- `Assets/Scripts/ResultManager.cs`
- `Assets/Scripts/SaveData.cs`
- `Assets/Scripts/SaveDataMigration.cs`
- `Assets/Scripts/SaveFileStore.cs`
- `Assets/Scripts/SceneLoader.cs`
- `Assets/Scripts/StageCatalog.cs`（新規）
- `Assets/Scripts/StageProgress.cs`
- `Assets/Scripts/StageRowUI.cs`（新規）
- `Assets/Scripts/StageSelectManager.cs`（新規）
- `Assets/Scripts/TutorialChoiceUI.cs`（新規）
- `Assets/Scripts/TutorialController.cs`（新規）
- `Assets/Scripts/TutorialData.cs`（新規）
- `Assets/Scripts/TutorialFocusUI.cs`（新規）
- `Assets/Tests/EditMode/BattleRewardTests.cs`
- `Assets/Tests/EditMode/GachaContentTests.cs`
- `Assets/Tests/EditMode/SaveMigrationTests.cs`
- `Assets/Tests/EditMode/SaveTests.cs`
- `Assets/Tests/EditMode/StageTutorialTests.cs`（新規）
- `Assets/Tests/EditMode/WordItemMigrationTests.cs`
- `Assets/Tests/PlayMode/StageTutorialSceneTests.cs`（新規）
- `Documentation/Phase10.2_ステージ進行・チュートリアル共通基盤レポート.md`（新規）
- `Tools/BuildStageTutorialUI.cs`（新規）
- `Tools/UIPlayValidation.cs`

新規meta 17件を含め計58ファイルです。既存meta・Prefab・画像・音声は変更していません。既存Sceneの編集はStageSelectだけで、旧オブジェクト・fileID・Inspectorイベントを維持しています。

主な新規型はStageDefinition/StageDatabase/StageBattleModifiers、StageCatalog、StageSession、StageClearRecord、StageSelectManager、StageRowUI、TutorialDefinition/TutorialStep/TutorialDatabase、TutorialRecord/TutorialProgressState、TutorialPreferences、TutorialCatalog、TutorialChoiceUI、TutorialController、TutorialFocusUI、DialogueLine/DialogueDatabase/DialogueCatalog、DialogueUIです。StageProgressは既存クラスを拡張しました。

## 2. ステージマスター

Assets/Resources/Data/stages.jsonをStageCatalogが読み込みます。stageId、stageNumber、displayName、enemyCharacterId、prerequisiteStageIds、firstClearCoins、repeatClearCoins、tutorialId、tutorialType、stageDescription、enemyBehaviorProfile、battleModifiers、isEnabledを持ちます。

重複ID・番号、未知の敵・前提ステージ、循環、負の料金、不明な行動プロファイルを検出します。Get/Allはコピーを返すため、UIから定義を書き換えません。同じ仕組みの通常ステージを追加する場合は、マスター追記だけで一覧へ反映されます。新しい種類の敵AI・チュートリアル操作自体には対応処理が必要です。

## 3. 初期ステージ・キャラクター

| stageId | 敵 | 解放条件 | 教材 |
|---|---|---|---|
| stage_1 | Technik | 初期 | 技・完全誘導あり |
| stage_2 | Adverb | stage_1クリア | 副詞・詳細誘導は今後 |
| stage_3 | Adjektiv | stage_2クリア | 装備/形容詞・詳細誘導は今後 |
| stage_4 | Modalverb | stage_3クリア | 助動詞・詳細誘導は今後 |
| stage_5 | Zukunft | stage_4クリア | Futur I・詳細誘導は今後 |
| feuer_practice | Feuer | 初期 | 従来戦闘・通常ステージ |

Zukunftは既存マスターを維持し、未登録だったTechnik/Adverb/Adjektiv/Modalverbの4種類を追加しました。計208種類です。4種類の暫定値はHP24、ATK5、DEF2、SPD4、レアリティ1、技sein/habenです。Technikは女性、残り3つは中性、すべてドイツ語・単数です。既存204種類の能力・画像指定・技は変更していません。

追加した名詞は普通のマスターデータなので、図鑑、辞書合成、文字ドロップ、全成功時の個体獲得へ参加します。取得個体へチュートリアル制約を保存しません。画像を既存キャラクターで代用せず、未作成時は会話の名前付きパネル・Battleの名前付きプレースホルダーを表示します。

StageSelectでは全6ステージをスクロール表示します。番号/名前、敵、状態、初回報酬、教材種別を表示し、未解放のボタンを無効にします。以前のボタンはイベントを維持したFeuer Practiceのショートカットとして残しています。

## 4. 解放とクリア記録

StageProgressStateの既存unlockedStageIds/clearedStageIdsを保持し、recordsへstageId、clearCount、firstClearRewardClaimed、lastBattleId、lastClearResultを保存します。最終クリア結果はVictoryです。敗北はこのクリア記録を更新しません。

解放は有効なマスターと前提クリアから判定します。古いunlockedStageIdsだけでロックを回避できません。StageSession.Select、CharacterSelect/Detailの開始、BattleManagerの開始、BattleRewardService.Beginの候補状態で再検証します。選択した敵を別の敵へ差し替える要求も拒否します。

SceneLoader.LoadStage1は新しいStage 1の3択へ接続し、従来のLoadBattleはFeuer Practiceへ接続しました。通常の既存Feuer直接戦闘も互換経路として維持し、この経路は従来どおり勝利100 Coinsです。

## 5. 初回クリア報酬と一括確定

ステージ経由の初勝利は通常100 + 初回300 = 400 Coins、再戦勝利は100 Coinsです。金額はstages.jsonの設定で、ガチャ・合成コストは変更していません。

BattleRewardServiceの保存済みチケットにstageIdと受講対象を保持します。Completeの同じPlayerStateTransactions候補内で、通常/初回報酬、文字列素材または新個体、クリア回数、初回受取フラグ、解放、必要なチュートリアル完了、確定済み戦闘記録を更新します。検証・保存成功後にメモリへ反映してResultへ進みます。

同じ戦闘IDを再送しても、通貨・初回報酬・クリア回数は増えません。従来の最新チケット方式と再起動後の二重付与防止を維持しています。保存失敗時は全体を未確定に保ち、同じ抽選結果で再試行できます。

Resultは保存結果の表示だけを行い、合計Coinsと通常/初回の内訳、クリアしたステージ、チュートリアル完了を追加表示します。勝敗背景・旧ボタンは維持しています。

## 6. 共通会話UI

Assets/Resources/Data/dialogues.jsonにdialogueId、speakerName、speakerSide、leftPortraitId、rightPortraitId、text、nextDialogueId、eventOnCompleteを定義します。DialogueCatalogは参照・循環・空文などを検査します。

DialogueUI.prefabは左右の立ち絵、話者名、下部テキストボックス、Nextを持ち、既存画像をResources参照できます。画像なしは名前付きパネルです。技名/敵名のトークン差し込みに対応します。長文は折り返してページ化し、連打は0.2秒間隔で抑制します。Next、テキストボックスのクリック、既存EventSystemの決定操作で進み、終了通知は1回です。

Play(dialogueId, ...)は連結会話、PlayLine(DialogueLine, ...)はNPCなどの単発会話に再利用できます。eventOnCompleteはコールバックへ渡し、文字列から任意コードを実行する仕組みにはしていません。

現在の台詞は既存UI・Latinフォントに合わせた英語です。日本語表示のためのフォント追加・翻訳は今回行っていません。

## 7. 3択・全スキップ・再受講

初回は「受講」「このチュートリアルだけスキップ」「今後すべてスキップ」の3択です。選択は保存成功してからCharacterSelectへ進みます。保存失敗時はその画面に留まります。

TutorialProgressStateはskipAllと、tutorialIdごとのcompleted、individuallySkipped、lastChoiceを保存します。ステージクリアとは別です。個別スキップは次の未選択チュートリアルをスキップしません。全スキップは将来の自動確認を省略します。

ステージ行のTutorialから明示的に再受講できます。初回の同ボタンは3択、既選択/クリア済み等の再受講は「受講」「通常戦闘」です。全スキップ中も明示的な受講が可能で、全スキップ設定自体は維持します。再受講で初回報酬は復活しません。

StageSelectのTutorial settingsとHomeのSettingsから全スキップを解除できます。解除しても個別の完了・スキップ記録を削除しません。

## 8. 入力制限・強調・エラー回復

TutorialControllerがステップに応じてBattleManagerの入力経路を許可/拒否します。技だけでなく副詞、müssen、未来形、解除、技パネルの開閉、SceneLoaderの遷移入口にも適用します。見た目だけを無効にする方式ではありません。

TutorialFocusUIの4枚の暗幕に対象ボタン部分の穴を開け、金色の枠で強調します。許可された元の技ボタンへ直接ポインターが届きます。他のボタンのイベントを差し替えて戦闘を複製していません。キーボードの決定対象も許可ボタンへ合わせます。

スキップ時はControllerを作らず通常操作です。未対応ステップ・データ不整合・会話中断等は未完了のまま検出し、暗幕・会話・入力制限を解除して通常戦闘へ戻します。Scene破棄時にも解除します。

## 9. Technikの動作

1. 3択の受講を保存し、選択個体・装備を保ったままBattleへ進む。
2. 共通会話で「技は動詞」「技を選んで攻撃」を説明する。
3. 選択個体の有効な装備技から1つを選び、そのボタンを強調する。装備検証では少なくとも1技が必須なので、存在しない固定技を要求しない。
4. 指定技の通常攻撃を既存のダメージ・文章・音声・演出経路で実行する。
5. 命中処理後の説明を表示する。一撃で倒しても説明を終えてから勝利確定へ進む。
6. 必要な追加攻撃を行って撃破する。
7. 通常/初回報酬・クリア・Stage 2解放・チュートリアル完了を一括保存する。

ステージ設定のguidedEnemyHp=30とguided_passiveを受講中だけ使います。敵は既存BattleContext.SkipEnemyTurnを使って待機し、指定操作中にプレイヤーが倒れません。マスターHP24や所有個体には適用しません。受講をスキップ/中断した場合は通常の敵行動へ戻り、完了後の通常再戦も通常戦闘です。

## 10. 残り4ステージ・今後の拡張

Adverb/Adjektiv/Modalverb/Zukunftは、順次解放、通常戦闘、初回/通常報酬、ドロップ、再戦、3択、スキップ設定まで実装しています。受講を選んでも現状は通常戦闘へ案内し、完了済みとして記録しません。

TutorialStepTypeはDialogue、Highlight、WaitForSkillSelection、WaitForAttackResolution、WaitForBattleVictory、Completeを実行できます。WaitForAdverbSelection、WaitForEquipmentChange、WaitForModalVerbSelection、WaitForFutureSelectionは型と拡張箇所を用意し、Supportedで明示的に未対応として拒否します。

StageSessionにstageId、受講状態、ステップ位置、対象技を保持し、CharacterSelect/CharacterDetail/Battle間で共有します。Sceneに属するControllerは破棄時に入力を解放し、戻った際に再接続できます。中断された攻撃待ちは安全のため最初から再開します。これにより装備誘導をScene間で扱う基盤がありますが、装備変更を待つ実ステップは次Phaseです。

## 11. セーブversion 6

既存StageProgressを拡張し、tutorialsを追加しました。v1〜v5からv6へ移行できます。v5のCoins、動的素材、確定済み戦闘報酬、個体ID・装備・図鑑・固定素材・convertedWordItemsを維持します。既知の旧クリアIDは1回クリア/初回報酬受領済みとして引き継ぎ、再付与を防ぎます。未知の旧進行IDも削除しません。

.v1.bak〜.v5.bak、通常バックアップ、recoveryの処理、原子的保存、4 MB上限を維持します。未対応v7以降では保存を停止して元データを保護します。v5からは初期Coinsを再付与しません。

途中の戦闘・会話位置はセーブに永続化せず、Scene間の実行時状態だけに保持します。アプリ再起動後は保存済みの完了/スキップ/設定から開始し、未完了の受講は最初からやり直せます。実セーブの移行は反映後の次回Play時です。

## 12. Edit Mode / Play Mode

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode | 164 | 0 |
| Play Mode | 54 | 0 |
| 合計 | 218 | 0 |

既存の全テストを含みます。新規検証は、初期ロックと直接アクセス拒否、全5ステージ進行、400/100 Coins、敗北、同じ戦闘IDの再送、通貨/素材/個体/進行/完了の保存失敗保護、3択、設定復元/解除、明示的な再受講、未実装の誤完了拒否を含みます。

Technikを実操作で完走し、誤った技/修飾/遷移の拒否、指定ボタンへのポインター到達、実攻撃後の説明、一撃勝利、保存再試行を確認しました。共通会話の左右画像・プレースホルダー・長文ページ化・連打・終了通知、Scene間のカーソル保持と制限解除、取得した教材名詞の通常戦闘/合成も検証しました。

v1〜v4の既存移行テストを維持し、v5のID・装備・0残高・動的素材・報酬記録・旧クリア保持、保護バックアップからの復旧を追加確認しています。

証跡：[Edit Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/edit-final.xml)、[Play Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/play-verified.xml)。

## 13. UI検証

1920×1080、1280×720、1600×1200、2560×1080で256枚を実描画しました。14,874項目成功、失敗0件です。

対象はStageSelect、CharacterSelect、Battle、Result、TutorialChoice、Dialogue、Settingsを含みます。ステージ一覧の上下スクロール、ロック、3択、未実装案内、再受講、全スキップ解除、長文会話の各ページ、立ち絵/本文の分離、指定技だけに届く暗幕の穴、初回報酬内訳、エラー後の通常操作を実描画しました。既存のガチャ・合成・装備・通貨・報酬UI検証も維持しています。

21 Scene/PrefabのGUID、fileID、ローカル/アセット参照、既存Inspectorイベントを検査しました。既存metaと画像/音声は不変です。

証跡：[UI結果](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/ui-verified/validation.txt)、[ステージ一覧](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/ui-verified/StageSelect-initial-locks-1920x1080.png)、[3択](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/ui-verified/TutorialChoice-first-visit-1280x720.png)、[技の強調](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/ui-verified/Battle-Technik-highlighted-skill-1280x720.png)、[初回報酬](/Users/isao/Documents/ChatGPT/ゲーム/phase102-validation/ui-verified/Result-Technik-first-clear-2560x1080.png)。

## 14. 残っている問題・範囲

今回の実装に未解決のテスト失敗はありません。UI検証の警告は、既存の不足音声8種類、Technik/Adverbの未作成画像2種類、保存失敗とチュートリアル中断の故意再現に伴う診断2種類です。画像はプレースホルダー、音声は既存フォールバックで処理します。長文会話の複数ページは仕様として検査し、省略扱いにはしていません。Unity内部SearchDatabase診断は0件でした。

残り4教材の詳細会話/誘導、装備変更など未対応ステップの実処理、日本語フォントと翻訳、案内役と追加キャラクターの正式画像/音声、アプリ全体の製品ビルド・実機配布は未実施です。再起動後の戦闘途中再開も対象外です。

通常の戦闘・Futur I・müssen・副詞競合・ダメージ計算・文章・音声を複製していません。ミッション・分解・強化・SPD順変更を追加していません。

## 15. Editorでの手動確認

1. 再インポート後にPlayする。旧セーブをバックアップしてv6へ移行し、残高・個体・装備・素材が維持されることを見る。セーブ削除/初期化は不要。
2. StageSelectを開き、Stage 1とFeuer Practiceが利用可能、Stage 2〜5がロック中であることを確認する。既存クリアがある場合はその進行に従う。
3. Stage 1で受講を選び、CharacterSelectからBattleへ進む。会話Next、強調された技、攻撃後の説明、追加攻撃、勝利を確認する。
4. Resultで合計400と通常100/初回300の内訳、Stage 2解放、受講完了を見る。再戦の勝利が100になることを確認する。
5. 個別スキップと全スキップを試し、報酬と解放が減らないことを見る。Tutorialから明示的に再受講でき、初回報酬が復活しないことを確認する。
6. StageSelectのTutorial settingsまたはHomeのSettingsで全スキップを解除する。残り4教材は通常戦闘として遊べ、未実装誘導が完了扱いにならないことを見る。
7. 4解像度で一覧スクロール、立ち絵と本文、指定技の枠、既存ボタンを確認する。保存失敗の故意再現は専用テストで行い、実プロフィールでは行わない。

元EditorのPlay停止をユーザー確認し、反映対象を検証済みファイルだけに限定しました。変更前ソースを退避し、既存差分・旧GUID/fileID・Inspectorイベント・既存204種類のマスター・確率・コスト・画像を照合しました。実プロフィール99ファイルの内容ハッシュ・作成日時・更新日時は作業前後で一致しています。
