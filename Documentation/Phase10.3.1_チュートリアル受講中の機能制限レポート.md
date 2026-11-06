# Phase 10.3.1 — チュートリアル受講中の機能制限

## 1. 調査結果

Phase 10.3のTutorialController.Allowsはステップ単位で対象操作だけを許可し、TutorialController.RefreshButtonsが実際のボタン状態を上書きしていました。BattleModifierUIは修飾操作全体を1つの判定で扱っており、müssenとFutur Iを個別に判定していませんでした。また、公開されているBattleSelectionに値を直接渡した場合について、攻撃確定前の機能別検査がありませんでした。

今回は既存のガイド状態とステージ番号を参照する小さな共通ポリシーを追加し、ステップの操作誘導と機能の紹介段階を分離しました。ガイドの進行、戦闘、装備準備の状態管理自体は既存の仕組みを使います。

## 2. 機能制限ポリシー

TutorialFeaturePolicyはSkill、Adverb、Adjective、Muessen、Futureを判定する状態を持たないクラスです。実行中のTutorialControllerの参照と既存StageDefinition.stageNumberから利用可能性を求めます。

制限は「現在のActiveガイドと選択ルートが一致」「受講中」「ガイドが有効」「未中断・未完了」「必須操作・結果説明の段階」「戦闘継続中、または装備準備が未確定」の場合だけです。保存された完了記録・クリア記録だけでは制限しません。新しい恒久アンロック、独立した進行状態、保存フラグはありません。

## 3. Stage 1〜5の機能

| 必須操作・説明中 | 技 | 副詞 | 形容詞の変更 | müssen | Futur I |
|---|---|---|---|---|---|
| 1 Technik | 可 | 不可 | 既存装備を維持 | 不可 | 不可 |
| 2 Adverb | 可 | 可 | 既存装備を維持 | 不可 | 不可 |
| 3 Adjektiv | 可 | 可 | 戦闘前の準備で可 | 不可 | 不可 |
| 4 Modalverb | 可 | 可 | 既存の戦闘前装備操作 | 可 | 不可 |
| 5 Zukunft | 可 | 可 | 既存の戦闘前装備操作 | 可 | 可 |

この表に加え、既存のステップ制限も満たす場合だけ、その時点の実操作を許可します。例えばStage 5でmüssenが紹介済みでも、Futur Iを選ぶ必須ステップではFutur Iだけを選べます。紹介段階の制限解除は、ステップ誘導の解除やmüssen/Futur I併用の許可とは別です。戦闘中の装備変更は追加していません。

## 4. 開始・終了・スキップ・自由戦闘

受講時と明示的な再受講時に、その場のガイドから判定を開始します。個別スキップ・全スキップでガイドがない場合は通常機能を使えます。再受講は従来どおり全スキップより優先できます。

必須操作と結果説明の終了後、WaitForBattleVictoryの自由戦闘へ移ると、Stage 1でも全機能の紹介段階制限を解除します。「ガイドを終了」、エラー解除、Scene離脱、戦闘終了でも解除します。再起動では既存のRuntimeInitialize処理がガイド参照を初期化するため、一時制限を復元しません。

解除は利用可能性と表示を切り替えるだけです。使用済みmüssen、予約済みFutur I、HP、ターン、確定装備を巻き戻しません。

## 5. UIと実行処理

BattleModifierUIはmüssen、Futur I、それぞれの解除、Clearを個別に判定します。未紹介の機能は無効なボタンと既存の暗転表示で示し、既存ヒント欄の下に機能名を含む説明を追加しました。無効なButtonに説明用クリックを割り当てる方式は使っていません。

説明はタッチやマウス入力を横取りしない表示専用テキストです。会話中は共通ハイライトUIごと非表示、自由戦闘では説明行を隠してヒント欄を元の高さへ戻し、終了時は共通UIと一緒に破棄します。Scene・Prefabに新しい参照を配線する必要はありません。

BattleManagerの既存選択APIと攻撃確定処理の双方で検査します。副詞の選択・解除、müssen/Futur Iの選択・解除を拒否し、BattleSelectionを直接変更しても、未紹介の修飾がある行動は副詞競合・選択履歴の確定・ダメージ処理より前に拒否します。拒否でターン、使用回数、予約、HP、保存状態は変えません。

Stage 3の個体ID確認、schwarzの選択・確定、下書き、Confirm/Back/Save/Discard/Cancel、保存失敗後の再試行は維持しました。通常のCharacterDetailには実行中の装備ガイドがないため、ステージ進行に関係なく従来どおり編集できます。確定済み形容詞の補正は全ステージで維持します。

## 6. 追加・変更ファイル

- [Assets/Resources/Data/ui_translations.json](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Resources/Data/ui_translations.json)
- [Assets/Scripts/BattleManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleManager.cs)
- [Assets/Scripts/BattleModifierUI.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleModifierUI.cs)
- [Assets/Scripts/TutorialController.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialController.cs)
- [Assets/Scripts/TutorialFeaturePolicy.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialFeaturePolicy.cs)
- [Assets/Scripts/TutorialFocusUI.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/TutorialFocusUI.cs)
- [Assets/Tests/EditMode/StageTutorialTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/StageTutorialTests.cs)
- [Assets/Tests/PlayMode/StageTutorialSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/StageTutorialSceneTests.cs)
- [Documentation/Phase10.3.1_チュートリアル受講中の機能制限レポート.md](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Documentation/Phase10.3.1_チュートリアル受講中の機能制限レポート.md)
- [Tools/UIPlayValidation.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/UIPlayValidation.cs)
- [Tools/validate_localization.py](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/validate_localization.py)

計12ファイル（新規meta 1点を含む）。新しい実行時クラスはTutorialFeaturePolicyのみです。

## 7. 日本語・英語

既存のLocalizationに説明文と機能名の6キーを追加しました。「müssen」「Futur I」と、ドイツ語の技・副詞・生成文章は翻訳しません。ヒントと説明を別行にし、説明文は小さめの文字で表示します。

日英335キーの構造・差し込み・参照検査と、Scene/Prefab 25点のGUID・参照・Inspectorイベント監査が成功しました。

## 8. 自動テスト

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode（既存199＋追加6） | 205 | 0 |
| Play Mode（既存80＋追加4） | 84 | 0 |
| 合計 | 289 | 0 |

[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/edit.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/play.xml)。

既存のStage 1〜5の実操作完了、日英、装備Scene遷移・個体別装備・保存失敗、通常攻撃・副詞競合・müssen・Futur I・文章生成、報酬・再戦、スキップ・部分リセット、ガチャ・合成・保存移行の検査を含みます。

追加の検査は、段階表、保存記録だけでは制限されないこと、全ステージでの確定装備保持、直接API呼び出しの拒否、BattleSelectionの直接変更からの不正確定防止、ガイド終了後の全機能復元、正当に使用したmüssen・予約攻撃の保持、戦闘終了・Scene離脱・再受講・通常の詳細画面を対象とします。

## 9. UI検証

1920×1080、1280×720、1600×1200、2560×1080を日本語・英語で検証しました。機能制限・解除・スキップの追加画面168枚、従来画面を含め合計932枚、45,651項目成功、失敗0件です。会話、実操作ハイライト、説明の文字切れ・重なり、レイキャスト、ガイド終了、自由戦闘、個別/全スキップでの表示と操作を検証しました。代表画像の目視確認も行っています。

[検査結果と警告一覧](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/ui/validation.txt)。

- [日本語・Stage 1の制限・1280×720](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/ui/ja-Features-Stage1-Restricted-1280x720.png)
- [日本語・Stage 3の装備準備・1600×1200](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/ui/ja-Features-Stage3-PreparationSelect-1600x1200.png)
- [英語・Stage 4の制限・2560×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/ui/en-Features-Stage4-Restricted-2560x1080.png)
- [日本語・ガイド終了後・1920×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1031-validation/ui/ja-Features-Stage1-GuideEnded-1920x1080.png)

## 10. データ保護とEditorでの確認

作業用コピーと隔離プロフィールで実行しました。実進行128ファイル・設定2ファイルの内容ハッシュ・作成日時・更新日時は作業前後で一致しています。元ソース793ファイルとGit状態が反映直前まで一致することも確認しました。元Editorの対象プロジェクト名とバージョンを照合し、Play停止を画面で確認してから反映しました。Scene・Prefab・既存metaとInspectorイベントは変更していません。自動生成された作業用フォントキャッシュは元の内容に戻し、持ち込みません。

進行セーブv6、UserSettings version1、世代切り替え、通常/部分リセットの意味は変更していません。ダメージ計算、副詞競合、müssenの即時2倍・戦闘1回、Futur Iの次ターン1.5倍、生成文章、敵AI、受講時のHP・待機条件、勝敗・報酬、ガチャ価格/排出率、合成価格/レシピも変更していません。

手動確認は次の順序が分かりやすいです。

1. Stage 1を受講し、副詞・müssen・Futur Iが無効で、ヒント欄に後のチュートリアルで紹介する旨が表示されることを確認する。
2. Stage 2で副詞、Stage 3でschwarz装備、Stage 4でmüssen、Stage 5でFutur Iの必須操作を行う。光る実ボタンが押せることを確認する。
3. 結果説明を読み終えて自由戦闘に入り、説明行が消え、通常機能が戻ることを確認する。Stage 4のmüssenは使用済みのままにする。
4. 別の受講で「ガイドを終了」を押し、副詞の変更・解除、müssen/Futur Iの通常選択を確認する。予約は「敵ターン→次の自分のターン開始時に発動→通常技」の順序を維持する。
5. 日英の両方で、個別スキップ・全スキップ・再受講と通常のCharacterDetailの装備編集を確認する。リセットの確認は専用プロフィールを使う。

## 11. Web版への注意・残る制限

新しい処理は通常のC#、Unity UI、TextMesh Proのみを使い、OS固有のファイル操作やネイティブAPIを追加していません。説明テキストのraycastTargetはfalseで、既存Buttonのクリック・タッチ入力を使います。Webビルドとスマートフォン専用UIは今回実行・追加していません。Phase 11.0では実Webビルドの入力・フォント・既存セーブ永続化を別途検証してください。

未解決のテスト・UI検査の失敗はありません。UI検証の警告22種類は、既存の不足画像/音声と意図したガイド終了・保存失敗の検証などです。文字切れ・グリフ不足の警告はありません。Unity Editor内部のSearchDatabase診断は検査結果のEDITOR_DIAGNOSTICS欄へ分離しています。チュートリアル途中の戦闘・ステップの再起動時復元は従来どおり行いません。
