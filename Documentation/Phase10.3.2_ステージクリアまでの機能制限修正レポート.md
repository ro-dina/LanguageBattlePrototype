# Phase 10.3.2 — ステージクリアまでの機能制限修正

## 1. 変更した解除条件

TutorialController.RestrictsFeaturesから、WaitForBattleVictoryを制限対象外にする条件を取り除きました。Phase 10.3.1のTutorialFeaturePolicyと紹介段階の表はそのまま再利用しています。必須操作・結果説明の完了では解除せず、受講中の戦闘が継続している限り制限します。

勝利・敗北などでBattleContext.OutcomeがOngoingでなくなった場合、ガイド終了/エラー中断、Scene離脱では従来の解除条件を使います。完了記録だけでは制限せず、明示的再受講時はその場のガイドに制限を適用します。保存フラグや新しいクラスは追加していません。

実行時コードの変更は、既存2ファイルの3行です。チュートリアル・戦闘エンジンを作り直していません。

## 2. WaitForBattleVictory中の動作

特定ボタンだけを有効にするTutorialController.RefreshButtonsのステップ制限は、この段階では適用しません。既存のEnter処理がハイライト・暗転・特定対象を外し、習った機能を自由に選べる状態へ戻します。一方、ステージ単位の機能制限と説明表示は維持します。

副詞カードはBattleManager.RefreshSelectionUIで、ターンの操作可否と既存AllowsInputの判定を毎回反映します。これによりStage 1では、次ターンのカード再生成後も副詞ボタンが無効のままです。助動詞・未来形のUIと攻撃確定時の検査は既存実装を再利用しています。

未紹介の修飾をBattleSelectionへ直接渡しても、攻撃確定前に拒否します。拒否によってターン、HP、予約、使用回数、報酬、選択履歴は変化しません。Stage 2以降の副詞選択・変更・解除、利用可能な通常技の選択は妨げません。

## 3. ステージごとの利用可能機能

以下は、必須操作・説明中から自由戦闘まで共通です。必須操作中は、この表に加えて従来の操作誘導も適用します。

| Stage | 技 | 副詞 | 形容詞装備 | müssen | Futur I |
|---|---|---|---|---|---|
| 1 Technik | 可 | 不可 | 確定済み装備の効果を維持 | 不可 | 不可 |
| 2 Adverb | 可 | 可 | 確定済み装備の効果を維持 | 不可 | 不可 |
| 3 Adjektiv | 可 | 可 | 戦闘前にschwarzを準備 | 不可 | 不可 |
| 4 Modalverb | 可 | 可 | 既存仕様 | 可 | 不可 |
| 5 Zukunft | 可 | 可 | 既存仕様 | 可 | 可 |

戦闘中の装備変更は追加しません。通常のCharacterDetailの編集機能は引き続き利用できます。Stage 4の使用済みmüssen、Stage 5の予約上限・併用禁止など、既存の使用条件は引き続き有効です。

## 4. 終了・スキップ・再受講

- 個別/全スキップ：ガイドがない通常戦闘として全機能を利用できます。
- ガイドを終了/エラー中断：同じ戦闘で機能制限を解除します。使用済みmüssenや予約済み攻撃を巻き戻しません。
- ガイド終了後の勝利：通常の勝利・進行・報酬として処理し、受講完了を新たに記録しません。
- 勝利/敗北/Scene離脱：制限とガイドUIを次の戦闘へ持ち越しません。
- 明示的再受講：そのステージの制限を再適用し、初回報酬の重複付与は防止します。
- 再起動：従来どおり途中の戦闘・細かいガイド段階は復元せず、一時制限を保存しません。

## 5. 変更ファイル

- [Assets/Resources/Data/dialogues.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/dialogues.json)
- [Assets/Resources/Data/tutorials.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/tutorials.json)
- [Assets/Resources/Data/ui_translations.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/ui_translations.json)
- [Assets/Scripts/BattleManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleManager.cs)
- [Assets/Scripts/TutorialController.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialController.cs)
- [Assets/Tests/EditMode/StageTutorialTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/StageTutorialTests.cs)
- [Assets/Tests/PlayMode/StageTutorialSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/StageTutorialSceneTests.cs)
- [Documentation/Phase10.3.2_ステージクリアまでの機能制限修正レポート.md](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Documentation/Phase10.3.2_ステージクリアまでの機能制限修正レポート.md)
- [Tools/UIPlayValidation.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/UIPlayValidation.cs)

計9ファイル。新規クラス・新規metaはありません。

TutorialFeaturePolicy、BattleModifierUI、TutorialFocusUI、装備管理、ステージの敵HP・待機設定、Scene・Prefab・既存metaは変更していません。

## 6. 日英の文章修正

Stage 1の自由戦闘ヒントを以下に変更しました。

- 日本語：「Technikを倒すまで、習った技を使って攻撃を続けましょう。」
- 英語："Keep using the skills you've learned until you defeat Technik."

Stage 2〜5の共通ヒントは「習った技や操作を使い、敵に勝利しましょう。」に変更しました。Stage 2の結果説明も、副詞の変更・解除と習った操作の利用を案内する内容に揃えました。Stage 3・4の結果説明は新仕様と矛盾せず、Stage 5は全機能が利用できるため、そのまま維持しています。

既存Localizationの3キーを修正し、キー数は335のままです。ドイツ語の技・副詞・生成文章を翻訳していません。未紹介機能の説明行はStage 1〜4の自由戦闘中も残し、Stage 5では未紹介機能がないため表示しません。

## 7. 自動テスト

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode（既存205＋追加1） | 206 | 0 |
| Play Mode（既存84を更新＋追加2） | 86 | 0 |
| 合計 | 292 | 0 |

[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/edit.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/play.xml)。

Stage 1の説明終了後と次ターンの制限、通常技による勝利、Stage 2〜5の日英チュートリアル完了、自由戦闘中の段階表、ハイライト解除、全装備技の操作可否、副詞の選択・変更・解除、不正な直接選択からの確定拒否を検証しました。

追加ケースでは、日本語Stage 1の自由戦闘からガイドを終了して通常勝利する流れと、Stage 2の自由戦闘中に敗北して完了・報酬を記録しない流れを確認しています。既存の通常戦闘・スキップ・再受講・装備・ガチャ・合成・保存移行・リセット・müssen・Futur I・副詞競合・文章生成の全テストも含みます。

日英335キーの参照・差し込み・会話ソース検査、Scene/Prefab 25点の参照・GUID・Inspectorイベント検査も成功しました。

## 8. UI検証

1920×1080、1280×720、1600×1200、2560×1080を日英で検証しました。自由戦闘・次ターン・勝利の重点画像120枚、既存画面を含む合計1020枚、48,641項目成功、失敗0件です。Stage 1の新しい文面、無効な副詞/助動詞/未来形、ハイライト解除後の技への実レイキャスト、通常攻撃による勝利、勝利後のガイド消去と再戦100 Coinsを確認しました。Stage 2〜5も同様に、自由戦闘の機能制限と技の操作、勝利まで検証しました。

[画面検査結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/ui/validation.txt)。代表画像の目視確認も行っています。

- [日本語・Stage 1自由戦闘・1280×720](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/ui/ja-Technik-FreePractice-1280x720.png)
- [英語・Stage 1次ターン・1600×1200](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/ui/en-Technik-FreePractice-NextTurn-1600x1200.png)
- [日本語・Stage 1勝利後・1920×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/ui/ja-Technik-PracticeVictory-1920x1080.png)
- [英語・Stage 2自由戦闘・2560×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1032-validation/ui/en-Lesson2-FreePractice-2560x1080.png)

作業用コピー・隔離プロフィールで実行しました。作業中に実セーブ本体とバックアップの2ファイルの更新を検出したため、開始時から不変とは扱っていません。元プロジェクトの非バッチEditorでの再生履歴と現在のPlay停止を確認し、最新の実進行132ファイル・実設定2ファイルのハッシュ・作成日時・更新日時を反映直前に記録しました。開始時と更新検出時の記録も保持しています。実セーブを巻き戻したり書き戻したりせず、反映前後の最新状態一致を検証します。自動テストとUI検証の起動引数はすべて専用の一時保存先を指定しています。元ソース796ファイルと既存Git状態は反映直前まで一致しています。Scene・Prefab・既存meta、敵HP/待機設定と戦闘ルールを保護し、生成フォントキャッシュも持ち込みません。

## 9. Unity Editorでの手動確認

1. Stage 1を受講して技の説明を終える。ハイライトが消え、通常技を選べる一方、副詞・müssen・Futur Iは使えないことを確認する。
2. 通常技を使って次ターンへ進み、副詞カードが有効に戻らず、未紹介機能の説明が残ることを確認する。Technikを倒すとガイドUIが消える。
3. Stage 2では副詞を変更・解除しながら戦い、müssen・Futur Iは使えないことを確認する。Stage 3ではschwarzの効果、Stage 4ではmüssenの使用済み表示とFutur Iの制限を確認する。
4. Stage 5では、従来どおり予約→敵ターン→次ターン開始時に予約発動→通常技の流れを確認する。
5. 別の受講で「ガイドを終了」を押し、同じ戦闘で通常機能が使えることを確認する。個別/全スキップ、日英切り替え、再受講も確認する。

## 10. Web版移行への影響と残る事項

変更は既存のC#条件式とUnity UIの有効/無効判定、Localization文面に限定しています。新しいネイティブAPI・ファイル操作・入力方式は追加していません。進行v6、UserSettings version1、通常/部分リセット、世代切り替え方式は維持しています。

Phase 11.0では、Webビルド上でも説明後と次ターンのボタン状態が維持されること、ガイド終了と戦闘終了で解除されることを同じ手順で確認してください。今回はWebビルドを実行していません。

未解決の自動テスト・UI検査の失敗はありません。画面検証の警告23種類は、既存の不足画像/音声と意図した保存失敗・ガイド終了の検証などです。文字切れ・グリフ不足の警告はありません。Unity Editor内部の検索診断はEDITOR_DIAGNOSTICS欄へ分離しています。
