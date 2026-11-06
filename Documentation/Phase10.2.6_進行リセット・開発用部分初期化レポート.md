# Phase 10.2.6 ゲームデータのリセット・開発用部分初期化

## 1. 調査したセーブ構造と初期状態

Unity 6000.4.8f1、進行セーブv6、独立したユーザー設定version1を確認しました。主な調査対象はSaveData/Validator、SaveFileStore、PlayerSaveService、PlayerStateTransactions、NewGameDefaults、GameManager、CharacterInstances、CharacterLoadoutStore、InventoryManager、StageProgress、TutorialPreferences、BattleRewardService、設定UI、SceneLoaderと既存テストです。

NewGameDefaults.Create()が新規ゲームの唯一の生成元です。CoinsはEconomySettings.Current.initialCoinsから取得し、現在は3,000です。初期個体は既存のNacht 1体で、GUIDを新しく発行します。マスターの初期装備を使用し、素材は空、図鑑はNacht発見済み、ステージ記録・チュートリアル・変換履歴・報酬チケットは空です。Stage 1とFeuer練習戦の利用可否は既存ステージマスターから導かれます。

既存SaveFileStoreは、本体の破損を保護し、復旧ファイル・バックアップ・旧バージョンのバックアップから回復します。この復旧探索をそのまま初期化後も使うと、以前の進行が戻る可能性があるため、リセット成功時だけ保存領域を切り替える仕組みを追加しました。

## 2. 実装したリセット

| 操作 | 対象 | 維持する進行 | 利用環境 |
|---|---|---|---|
| 最初からやり直す | 進行全体をNewGameDefaultsに戻す | なし | 製品版を含む全環境 |
| チュートリアル記録を初期化 | 受講/スキップ/再受講・全スキップ設定 | ステージ、Coins、個体、図鑑、装備、素材、変換履歴 | Editor / Development Build |
| ステージ進行を初期化 | 解放/クリア回数/初回クリア記録 | チュートリアル記録、Coins、個体、図鑑、装備、素材、変換履歴 | Editor / Development Build |
| 開発用：完全初期化 | 通常リセットと同じ生成・保存処理 | なし | Editor / Development Build |

**すべての操作で、戦闘報酬の最新記録・未完了チケットと予約された付与計画を無効化します。獲得済みのCoins・所持品は、部分初期化では保持します。** 確認画面にもこの影響を明記しています。

ステージだけの初期化では受講記録・全スキップ設定を保持します。初回クリア報酬は再取得可能です。チュートリアルを繰り返したい場合は、チュートリアル記録の初期化、または既存の各ステージの再受講を使えます。

回数制限、待機時間、ペナルティはありません。同じ確認操作の重複だけをPlayerOperationで拒否し、新たに確認した操作は何度でも実行できます。新しい初期キャラクターやAnfängerは追加していません。

## 3. 新規・変更ファイル

- `Assets/Resources/Data/ui_translations.json`
- `Assets/Resources/UI/ProgressResetDataUI.prefab`
- `Assets/Resources/UI/ProgressResetUI.prefab`
- `Assets/Scripts/CharacterInstances.cs`
- `Assets/Scripts/PlayerSaveService.cs`
- `Assets/Scripts/PlayerStateTransactions.cs`
- `Assets/Scripts/ProgressResetAccess.cs`
- `Assets/Scripts/ProgressResetDataUI.cs`
- `Assets/Scripts/ProgressResetKind.cs`
- `Assets/Scripts/ProgressResetService.cs`
- `Assets/Scripts/ProgressResetUI.cs`
- `Assets/Scripts/SaveFileStore.cs`
- `Assets/Scripts/SaveGenerationStore.cs`
- `Assets/Scripts/SceneLoader.cs`
- `Assets/Scripts/SettingsInstaller.cs`
- `Assets/Scripts/SettingsUI.cs`
- `Assets/Tests/EditMode/ProgressResetFileTests.cs`
- `Assets/Tests/PlayMode/ProgressResetSceneTests.cs`
- `Assets/Tests/PlayMode/SettingsSceneTests.cs`
- `Documentation/Phase10.2.6_進行リセット・開発用部分初期化レポート.md`
- `Tools/BuildProgressResetUI.cs`
- `Tools/UIPlayValidation.cs`
- `Tools/validate_localization.py`
- `Tools/verify_reset_build_flags.py`

計34ファイル（新規meta 10点を含む）。

新しい主な型はProgressResetKind、ProgressResetAccess、ProgressResetRequest、ProgressResetService、ProgressResetDataUI、ProgressResetUI、SaveGenerationStoreです。

既存のScene/Prefab/metaは変更していません。設定の既存データタブへ新しい共有Prefabを実行時に配置し、以前の「追加予定」案内を非表示にしています。Inspector参照、GUID、fileID、既存イベントは維持しました。

## 4. リセット対象と設定の維持

通常・開発用完全初期化は、WalletState、所有個体、装備上書き、素材と動的素材、図鑑の発見、ステージ記録、チュートリアル/全スキップ設定、報酬チケット、旧単語変換履歴を新規状態へ戻します。

表示言語・マスター/BGM/効果音/ボイス音量はUserSettingsに残します。リセット処理は設定の保存APIを呼びません。テストでは設定ファイルの内容と更新日時が維持されることを確認しました。

保存成功後に進行の各ストアを既存の一括適用で更新し、選択個体の整合性を回復します。StageSession・チュートリアルの実行カーソル、直前画面・敵・勝敗の一時状態も整理します。UIはSceneLoaderからGermanyHomeへ戻り、古い画面の選択/下書き/結果を破棄します。成功表示はホームで出し、ユーザーの選択した言語のまま続行できます。

## 5. 保存・バックアップ・失敗・再起動

ゲームの進行形式はv6のままです。進行フォルダ内の`save-generation.json`（選択情報version1）だけを追加します。初回リセット前は従来の`save.json`をそのまま使います。

リセットの流れ：

1. 非戦闘Scene、戦闘個体ロックなし、会話/保存処理中でないこと、書き込み可能なセーブであることを確認。
2. 初期化対象の候補を生成し、SaveDataValidatorと個体参照を検証。
3. 新しい`progress-<GUID>/`へ、既存SaveFileStoreで本体と同じ状態のバックアップを保存。
4. 本体・バックアップを読み戻し、候補との一致を確認。
5. 一時ファイルへ書いた選択情報を原子的に置換。ここを成功確定点とする。
6. 保存成功後にメモリへ一括適用し、ホームへ遷移。

リセット前の本体・復旧ファイル・旧移行バックアップ・以前の保存領域は削除しません。選択情報の切り替え後は、新しい領域内だけを復旧対象にするため、古い進行が自動で戻りません。新しい本体が破損しても、新しいバックアップまたはその領域の復旧ファイルを使います。

候補保存または選択情報の確定に失敗した場合、元の本体・バックアップ・選択情報・メモリは維持します。確定前の候補領域と一時ファイルは復旧対象にしません。失敗画面から再試行できます。

確定前に終了した場合は元の保存先、確定後に終了した場合は新しい保存先を選びます。リセット後の再起動でも個体IDは安定します。選択情報が破損・未対応形式、または選択先がない場合は、古い保存先へ戻さず書き込みを禁止して保護します。古いSaveFileStoreインスタンスによる保存も、選択先が変わっていれば拒否します。

以前の保存領域は手動修復用の保管資料であり、ゲーム内からリセットを取り消す機能はありません。自動削除・保管数制限は今回追加していません。

## 6. 報酬チケット・戦闘GUID・実行条件

通常・部分初期化のどちらもBattleRewardStateを空にし、既存Restoreを通して保存再試行の付与計画も解放します。古い未完了/処理済みGUIDでCompleteを呼んでも付与しません。新しい戦闘は既存のGUID生成と報酬確定処理をそのまま使用します。

Futur Iの戦闘コンテキストを持つSceneや、個体の戦闘ロックがある間は初期化を拒否します。設定入口のないScene、会話中、PlayerStateTransactionsの処理中、保護されたセーブも拒否します。UIの表示制限に加え、実行APIとトランザクション側で再確認します。

Resultは既に保存済みの報酬を表示するSceneです。報酬確定はBattle側の保存完了後にResultへ移る既存方式を維持しています。未完了処理中にResultから重ねて初期化する経路はなく、保存処理中の呼び出しも拒否します。

## 7. Editor / Development / 製品版

ProgressResetAccessを実際のプリプロセッサー定義でコンパイルし、処理のコールバックが呼ばれるか検証しました。

| 定義 | 通常リセット | 開発用3種類 | 実処理の呼び出し数 |
|---|---|---|---:|
| Release | 許可 | 拒否 | 1 |
| DEVELOPMENT_BUILD | 許可 | 許可 | 4 |
| UNITY_EDITOR | 許可 | 許可 | 4 |

未知の種類は全構成で拒否します。[検証結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/build-flags/results.txt)。

製品版の通常リセットは制限しません。開発用ボタン群は非表示にし、開発用種類を実行APIへ渡しても拒否します。UIだけの判定には依存していません。

## 8. 日本語・英語のUI

既存Localizationへ22キーを追加しました（合計306キー）。データタブ、通常/部分/完全初期化、確認対象の説明、確認チェック、実行中、キャンセル、保存失敗、成功、続行を日英で表示します。学習用のドイツ語データは変更していません。

確認画面は、初期化範囲と取り消せない旨を示し、チェックを入れるまで実行できません。初期フォーカスはキャンセルに置きます。保存中は確認・キャンセル・再実行を止め、成功時だけホームの成功表示へ進みます。成功表示のフォーカスは続行です。

確認画面はCanvas 295で、設定画面（270）の上に表示します。背後の設定/ゲーム画面をCanvasGroupと全面の遮断画像で操作不可にします。閉じる時とScene終了時に状態を復元します。Scene読込時に後から生成される通貨HUDも遮断対象です。

## 9. テスト結果

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode（既存180＋追加12） | 192 | 0 |
| Play Mode（既存62＋追加6） | 68 | 0 |
| 合計 | 260 | 0 |

[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/edit-final.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/play-final.xml)。旧移行・破損復旧・Futur I・müssen・副詞競合・ガチャ・合成・個体装備・Technikの既存テストをすべて含みます。

新規検証：

- 初期状態への復帰、個体IDの更新と再起動後の安定、設定ファイル内容/日時の維持。
- 通常リセット後のガチャ・合成・実戦闘・初回400 Coins、同一戦闘の重複付与防止。
- 未完了/処理済みの旧戦闘チケットの拒否。
- チュートリアルだけ、ステージだけの初期化と対象外セクションの一致。ステージ初期化後の初回報酬再取得。
- 未チェック・キャンセル・同じ要求の再実行、戦闘Scene/個体ロック/保存中/未対応セーブからの拒否。
- 保存の確定に失敗した際の本体/バックアップ/メモリ/画面維持、再試行成功。
- 確定前の中断ファイルの無視、確定後の再起動、新しい本体破損時の復旧、旧復旧/移行バックアップの除外。
- 選択情報の破損/未対応形式/不正パス/保存先消失の保護、古いライターの拒否。
- 日英の表示、重なったモーダルの背後操作禁止、成功時のホーム/続行フォーカス。

旧テストの「データタブには追加予定の案内だけがある」は、新しい確認付きリセット項目があり、開いただけではデータを変えない検証へ更新しました。

## 10. UI検証

1920×1080、1280×720、1600×1200、2560×1080を日英で検証しました。既存画面を含む472枚の描画、30,052項目成功、失敗0件です。データタブ、未チェック/チェック済み、3種類の開発用確認、通常/部分初期化の成功、保存失敗と再試行を操作しました。文字切れ・グリフ不足・操作の重なりの警告は0件でした。

[画面検査結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/ui/validation.txt)。Scene/Prefabの参照監査25点と、日英306キーの検査も成功しています。

- [日本語のデータタブ・1280×720](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/ui/ja-Reset-Data-1280x720.png)
- [確認チェック済み・1600×1200](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/ui/ja-Reset-Confirmation-Checked-1600x1200.png)
- [英語の保存失敗画面・1920×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/ui/en-Reset-Failed-1920x1080.png)
- [成功後のホーム・2560×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1026-validation/ui/ja-Reset-Full-Success-2560x1080.png)

未解決のゲーム側テスト失敗はありません。UI検証の警告14種類は既存の不足音声8種類、不足画像2種類、意図した報酬保存失敗・ガイド中断・会話中断・リセット保存失敗の4種類です。別にUnity Editor内部のSearchDatabase初期化でArgumentOutOfRangeExceptionが1種類出たため、ゲーム側の失敗と分けて記録しました。

フルの配布用ビルドやWebGLの永続化は今回実行していません。製品版/開発版の制限は実ソースを各定義でコンパイルして検証しています。

## 11. Unity Editorで人間が確認する操作

1. 専用のテスト用プロフィールでPlayし、設定→データを開く。通常リセットと開発用3項目が表示されることを確認。
2. 「最初からやり直す」で説明を読み、未チェックでは実行不可、キャンセルでは進行不変を確認。
3. 確認チェック後に実行し、保存成功表示とGermanyHomeへの復帰を確認。Coins 3,000、Nacht 1体、空の素材と初期ステージに戻ることを確認。
4. 表示言語・4音量が保たれること、Play停止/再開後にも新しい進行と個体IDが保たれることを確認。
5. チュートリアルだけの初期化ではステージ/Coins/個体/装備を維持し、受講選択が戻ることを確認。
6. ステージだけの初期化では受講記録を維持し、Stage 1からの進行と初回報酬を再確認。
7. ガチャ、文字列合成、通常/Futur I/müssenの戦闘、Technikの操作ガイドを確認。
8. Development Buildでは開発用項目、通常ビルドでは通常リセットだけが表示されることを実機ビルドでも確認。

実プレイヤーのプロフィールは今回リセットしていません。手動で通常リセットを実行すると、そのプロフィールの進行が初期化されます。

## 12. Web版・文化祭展示への注意点

NewGameDefaults（生成）、PlayerStateTransactions/SaveFileStore（検証・確定）、UserSettings（維持）を分離しています。別の展示用初期状態を将来作る場合は、候補生成だけを追加し、同じ確定処理へ渡せます。今回は展示用プリセット・Web公開・引き継ぎを追加していません。

リセット後の有効な保存先はSaveFileStore.MainPathから取得してください。ルートの`save.json`を直接読んだり、旧バックアップを無条件に復元する処理を追加しないでください。設定は従来の独立フォルダのままです。

将来の引き継ぎにはPlayerSaveService.Capture()で有効な進行を取得し、独立設定の扱いを別に決める方法が適します。WebGLでの永続化完了通知やクラウド間の競合制御は将来の保存層の仕事です。今回のリセット処理はOS固有パスやネイティブAPIへ新規依存せず、既存と同じ.NETのファイル操作を使用しています。

作業用コピーと専用プロフィールで検証し、実進行122ファイル・実設定2ファイルの内容ハッシュ・作成日時・更新日時を作業前後で照合しました。元ソース764ファイルと既存Git差分は反映直前まで一致しており、既存の`.DS_Store`差分は保護しています。元EditorはPlay停止済みとの回答を受けてから反映します。価格・確率・レシピ・マスター能力・学習データ・既存の画像/音声/Scene/Prefab/metaを変更していません。
