# Phase 10.3 — Stage 2〜5 操作チュートリアル完成レポート

## 1. 既存基盤と実装方針

既存のStage 1と同じTutorialController、TutorialFocusUI、DialogueUI、TutorialChoiceUIを使用しました。技の受付、BattleContext.Apply後の攻撃結果、確定済み個体装備を確認して進行します。新しい戦闘エンジンや永続チュートリアル状態は追加していません。

BattleManagerは選択と攻撃適用後の通知、CharacterDetailManagerは既存の装備操作と確定後の通知を担当します。表示だけでなく、各操作の実行メソッドでも受講中の入力を制限します。不正なターゲット・要件・未対応ステップは明示的に拒否し、実行時の問題はガイドを解除して通常操作へ戻します。

## 2. Stage 2〜5の流れと完了条件

| Stage | 必須の実操作 | 実状態による確認 | その後 |
|---|---|---|---|
| 2 Adverb | 実在する最初の副詞カード→技 | 選んだIDが行動に入り、適用した攻撃文に副詞が含まれる | 副詞変更・解除を含む自由戦闘 |
| 3 Adjektiv | 戦闘前に形容詞一覧を開く→schwarzを選ぶ→確定 | 同じ個体IDの確定装備と実際の戦闘装備が一致する | 装備付き攻撃を体験し、自由戦闘 |
| 4 Modalverb | müssen→技 | 実際の即時攻撃と使用済み状態 | 2倍攻撃の文章・ダメージを確認し、自由戦闘 |
| 5 Zukunft | Futur I→技→次ターンの発動確認→通常技 | 予約0ダメージ、予約の消費、実際の予約攻撃、続く通常攻撃 | 文章・ダメージを確認し、自由戦闘 |

全ステージとも、必要な操作と結果説明を終えた後、実際に勝利した場合だけ、既存の報酬保存と一緒に完了を記録します。説明前の不正な勝利や中断は完了にしません。過去の完了記録がある再受講で敗北した場合、その過去の記録は消しません。

Futur Iの順序は既存実装に従います。「予約→敵ターン→次の自分のターン開始時、入力より先に予約発動→同じターンに通常技」です。予約と発動の間に別の通常入力を挟む変更はしていません。

## 3. 装備Scene遷移と個体の保持

Stage 3を受講して開始すると、CharacterSelectから既存のCharacterDetailへ移動します。戦闘開始前の準備なので、戦闘中の装備変更は追加していません。

実行中のStageSessionに個体ID・選択済み・確定済みの状態を持たせ、通常のConfirmで選択画面へ戻る場合と、詳細画面の戦闘開始ボタンで直接進む場合を両方扱います。別個体に切り替えた場合や確定装備が変わった場合は準備をやり直します。戦闘へ進むと準備会話の後から再開し、装備を検証してから攻撃を教えます。

現在の装備画面は、所有素材数によらず登録済み形容詞を選べる既存仕様です。そのため試用装備・無料素材・新しい装備所有ルールは不要でした。schwarzの選択は下書き、確定は通常の個体装備の保存です。保存した装備は受講終了後も残る、プレイヤーが選んだ通常装備です。別個体・マスターは変更しません。Back、Save、Discard、Cancelを維持し、中断で確定前の下書きを勝手に保存しません。準備の確定には既存のPlayerStateTransactionsによる原子的保存を使います。保存に失敗した場合は、確定済み装備・ディスク上の装備を変更せず、編集下書きと準備画面を維持し、再試行後に進めます。

## 4. 受講時だけの戦闘条件

新しいTutorialBattleSetupは敵マスターをコピーし、ステージデータに指定した受講時HPを適用します。Stage 2〜4は最低40、Stage 5は最低30です。さらに、既存DamageCalculatorを使って最初の必須攻撃を生き残るだけのHPを確保します。必要操作を終えて自由練習になるまで敵は待機し、その後は既存の敵攻撃に戻ります。プレイヤーの能力・ダメージ計算・無敵処理は変更していません。

Stage 2の必須体験中だけ、敵は副詞を選びません。副詞競合で体験が成立しない事態を防ぐためであり、競合の計算ルールはそのままです。自由練習では従来の副詞選択・競合が再開します。

スキップした戦闘ではこれらの条件を使わず、元の敵マスターと通常ルールで戦います。図鑑・合成・取得個体のAdverb、Adjektiv、Modalverb、Zukunftの基本能力は変更しません。

## 5. スキップ・再受講・中断・リセット

初回の3択、個別スキップ、保存される全スキップ、設定からの変更、全スキップより優先する明示的再受講を維持しています。再戦でも初回報酬は重複しません。

操作ハイライト中には共通の「ガイドを終了」ボタンを追加しました。Scene破棄、会話中断、エラー、中断ではハイライトとボタン状態を解除します。装備準備の終了は通常の装備編集に戻り、未確定の下書きは従来の保存・破棄・キャンセルで扱います。

チュートリアルだけのリセットで受講状態が消え、解放済みステージを再び受講できます。ステージだけのリセットでは解放順を初期状態へ戻し、受講記録は保持します。進行v6、設定version1、世代切り替え方式は変更しません。

## 6. 日英対応

新規会話12件、操作ヒント8件、共通表示3件を既存の翻訳テーブルへ追加しました。技・副詞・形容詞・実際のダメージ・実際に生成されたドイツ語文章を会話へ差し込みます。ドイツ語の学習対象語と文は翻訳しません。

日英329キーの構造・差し込み・実行時参照検査が成功しました。Scene/Prefabの参照監査25点も成功しています。

## 7. 追加・変更ファイル

- [Assets/Resources/Data/dialogues.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/dialogues.json)
- [Assets/Resources/Data/stages.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/stages.json)
- [Assets/Resources/Data/tutorials.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/tutorials.json)
- [Assets/Resources/Data/ui_translations.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/ui_translations.json)
- [Assets/Scripts/BattleManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleManager.cs)
- [Assets/Scripts/BattleModifierUI.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleModifierUI.cs)
- [Assets/Scripts/CharacterDetailManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterDetailManager.cs)
- [Assets/Scripts/CharacterSelectManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterSelectManager.cs)
- [Assets/Scripts/DialogueUI.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/DialogueUI.cs)
- [Assets/Scripts/StageCatalog.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/StageCatalog.cs)
- [Assets/Scripts/TutorialBattleSetup.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialBattleSetup.cs)
- [Assets/Scripts/TutorialController.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialController.cs)
- [Assets/Scripts/TutorialData.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialData.cs)
- [Assets/Scripts/TutorialFocusUI.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialFocusUI.cs)
- [Assets/Tests/EditMode/StageTutorialTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/StageTutorialTests.cs)
- [Assets/Tests/PlayMode/StageTutorialSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/StageTutorialSceneTests.cs)
- [Documentation/Phase10.3_残り4ステージのチュートリアル完成レポート.md](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Documentation/Phase10.3_残り4ステージのチュートリアル完成レポート.md)
- [Tools/UIPlayValidation.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/UIPlayValidation.cs)
- [Tools/validate_localization.py](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/validate_localization.py)

計20ファイル（新規meta 1点を含む）。

新規の実行時クラスはTutorialBattleSetupのみです。既存のScene、Prefab、既存metaのGUIDとfileID、Inspectorイベントは維持しています。

## 8. テスト

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode（既存192＋追加7） | 199 | 0 |
| Play Mode（既存68＋追加12） | 80 | 0 |
| 合計 | 279 | 0 |

[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/edit-final.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/play-final.xml)。

既存の保存移行・破損復旧・個体装備・ガチャ・合成・報酬の原子的保存・Futur I・müssen・文章生成・音声フォールバック・副詞競合・設定・リセット・Technikのテストを含みます。旧「Stage 2〜5未実装」の検査は、実装済み・正しい操作対象の検証へ更新しました。

追加検証は、日英での各ステージの実操作と勝利、誤入力による進行禁止、素材不変、個体装備の独立、準備のScene移動、下書きの取消/破棄、中断、装備保存失敗の再試行、0ダメージ予約・次ターン発動・通常行動、müssenの使用制限、ガイド終了後の敗北、部分リセット後の再受講を対象にしています。

## 9. UI検証

1920×1080、1280×720、1600×1200、2560×1080を日英で検証しました。新レッスン292枚、既存画面を含む合計764枚の描画、39,097項目成功、失敗0件です。新レッスンの会話、副詞・形容詞・助動詞・未来形・技の実ボタンのハイライト、確定前の装備、Scene移動後の再開、結果説明、自由練習、勝利結果を含みます。

[画面検査結果](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/ui/validation.txt)。ボタンの画面内配置、入力対象への実レイキャスト、他の操作の無効化、操作ヒントの文字切れを検査しています。実画像の代表例も目視確認しました。

- [日本語の副詞選択・1280×720](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/ui/ja-Lesson2-AdverbHighlight-1280x720.png)
- [日本語の形容詞選択・1600×1200](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/ui/ja-Lesson3-ChooseAdjectiveHighlight-1600x1200.png)
- [英語のmüssen選択・2560×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/ui/en-Lesson4-ModifierHighlight-2560x1080.png)
- [日本語の予約攻撃説明・1920×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase103-validation/ui/ja-Lesson5-FutureFired-1920x1080.png)

## 10. データ保護

作業用コピーと隔離プロフィールで検証しました。実進行128ファイル・実設定2ファイルについて内容ハッシュ・作成日時・更新日時が作業前後で一致しています。元ソース790ファイルと既存Git差分の一致も反映直前に確認しました。既存のAnfänger画像metaとRecovery Sceneの未追跡ファイルを保護しています。元EditorがPlay停止中であることは、対象バージョンと元プロジェクト名を画面で照合して確認しました。

価格・排出率・合成レシピ・単一Coins・キャラクター個体管理・既存装備/素材マスター・辞書/文法処理・予約/助動詞ルール・文字ドロップ40%・Battle GUIDと二重付与防止・セーブリセット方式・言語/音量設定は変更していません。通常勝利100、初回追加300、再戦100は既存処理のままです。

## 11. Unity Editorでの確認操作

1. StageSelectでStage 2〜5を順に受講し、会話→光る実ボタン→結果説明→自由戦闘→勝利の流れを確認する。
2. Stage 3では選択したNacht個体のschwarzを確定する。同種の別個体の装備に影響しないこと、Confirmで選択画面へ戻っても準備済みを維持することを確認する。
3. Stage 4では使用後のmüssen表示とClearで回数が戻らないことを確認する。
4. Stage 5では予約時0ダメージ、次ターンの1.5倍発動、その後の同じターンの通常技を確認する。
5. 日英を切り替えて再受講し、個別スキップ・全スキップ・ガイド終了を試す。
6. リセットの操作確認は専用プロフィールで行う。実プレイヤーの記録を確認目的で初期化しない。

## 12. 制限・残る事項

未解決のゲーム側テスト・画面検査の失敗はありません。UI検証の警告21種類は、既存の不足音声/画像と意図的な保存失敗・ガイド中断などです。詳細は画面検査結果のWARNINGS欄に記録しています。Unity Editor内部のSearchDatabase診断はEDITOR_DIAGNOSTICS欄へ分離しています。配布用フルビルドは今回実行していません。

Stage 2〜5の敵専用画像や一部文章/単語音声は既存素材が未整備です。従来の準備中表示・会話の立ち絵プレースホルダー・単語音声へのフォールバックを使い、不足素材をこの実装で新規制作してはいません。

受講途中の細かいステップと進行中の戦闘は従来どおりセーブしません。アプリ再起動後は受講を最初から開始します。Sceneをまたぐ装備準備は実行中のStageSessionで保持します。ドイツ語以外の個体で助動詞・未来形レッスンを開始した場合はガイドを解除して通常戦闘へ戻ります。Nachtでの全レッスンを検証対象にしました。WebGL、クラウドセーブ、展示用機能、新しい初期キャラクターは追加していません。
