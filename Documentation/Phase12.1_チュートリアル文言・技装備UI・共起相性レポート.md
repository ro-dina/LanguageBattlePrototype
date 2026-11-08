# Phase 12.1 実装・検証レポート

対象：`/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype`。以下の変更ファイルはこのルートからの相対パスです。

## 実装状況

チュートリアル表示と旧副詞装備UIの整理、および共起順位を受け取る相性判定・戦闘接続を実装しました。**実コーパスの共起順位データの接続・実データでの有利／不利の照合は未完了**です。現在のプロジェクトには名詞別の共起スコア・頻度・品詞を持つデータがなく、元データの所在を問い合わせています。キャラクターの`skillIds`を根拠なく共起順位として流用していません。指定のフォールバックに従い、現在の未収録名詞は通常相性になります。

## 1. チュートリアルの英語表示

既存の日本語訳355キーにはStage 1〜5の会話・ヒントの訳が含まれていました。一方、`TutorialController`から`TutorialFocusUI`へ英語の`hint`を渡して`TMP_Text.text`に直接設定する経路、`TutorialChoiceUI`の選択肢・説明を英語で直接設定する経路があり、自動的な原文照合・後続フレームでの更新に依存していました。すべての画面で常時英語になることまでは再現していません。確認できた不安定な経路を、初回描画からキー指定する方式へ変更しました。Webブラウザでは、動的生成したStage一覧のChallenge / Tutorial / Lockedと共通Settingsボタンに英語が残ることも再現したため、これらと、新規生成される受講画面のClose・会話のNextにも既存のキーを明示しました。自動変換への依存だけではWebで表示が揃わない事象を確認したもので、TMP内部通知の不達などを断定したものではありません。

- `tutorials.json`の19個のヒントに既存訳への`hintKey`を追加。
- `dialogues.json`の15会話に`localizationKey`を追加。会話順・イベント・本文の学習内容は変更なし。
- 選択肢、ガイド名、画像未準備表示、説明、自由戦闘ヒント、Stage一覧ボタン、共通設定入口は`LocalizedText`で表示。
- 表示言語変更時に文章だけ更新し、進行カーソル・HP・ラウンド・選択内容を維持。会話・必須操作中の切り替えはUserSettings経由のテストで確認。現行Battle Sceneには設定入口がなく、この変更で新しい入口は追加していません。既存の移動制限も保持。
- ドイツ語の技名、müssen、Futur I、schwarz、例文は保持。
- 既存355訳はすべてそのまま保持し、画面見出し・技選択・通常相性など7キーを追加（合計362）。

## 2. 副詞装備UIの変更

`CharacterDetailManager`の既存4スロット・選択パネル・ボタンPrefabを再利用し、副詞一覧を動詞技の一覧へ置き換えました。登録済み技の変更・追加（最大4技）・削除を扱い、重複登録と最後の1技の削除を拒否します。個体の下書き、Save / Confirm / Discard / Cancel、形容詞・冠詞装備は従来の流れを使用します。未所有の閲覧では編集できません。

技ラベルと選択・合成プレビューから旧副詞の接頭表示を除き、説明を「副詞は戦闘中に選択」に変更しました。古い`OpenSkillEquipmentList`はInspector互換のため同名で残し、技選択画面を開きます。旧`EquipSkillEquipment`と`GameManager.SetSkillEquipment`は何も変更しない互換入口として残しました。

## 3. 旧副詞装備データとの互換性

セーブv6の`skillSlots[].equippedAdverbId`は残しています。読み込み・閲覧・形容詞のみの保存で旧値を削除しません。技を明示的に削除した場合だけ、そのスロットの付随データも取り除き、残るスロットとの対応を維持します。

戦闘用に複製する個体スナップショットでは旧副詞値を空にし、`BattleManager`はプレイヤー・敵とも旧副詞装備を読み込みません。副詞カード候補も旧装備を根拠に増減させません。戦闘中の候補選択・変更・解除、副詞なしの攻撃は維持しています。現在の登録副詞はschnellとsehrで、従来の双方由来の候補・競合ルールを維持します。

## 4. 共起データの調査結果と受け口

現在のキャラクターマスター208件には`skillIds`がありますが、装備初期技の配列です。共起スコア、頻度、品詞、コーパス出典、順位定義は収録されていません。技マスターは24件です。既存の`collocationAdverbIds`は任意の戦闘副詞候補であり、名詞と動詞の順位データではありません。変更前の相性は文法性の三すくみでした。調査記録は検証ディレクトリの`cooccurrence-audit.json`にあります。

追加した受け口は`CharacterData.verbCollocations`、または任意の`Resources/Data/noun_collocations.json`です。後者は次の構造を想定します。

```text
nouns[]
  characterId: 既存のキャラクター種類ID
  entries[]
    lemma: 技マスターの不定詞と対応する動詞基本形
    partOfSpeech: VERB / AUX / verb またはドイツ語VV・VA・VM系タグ
    score: 値が高いほど共起関係が強いスコアまたは頻度
```

このJSON資産自体は作成していません。データ入手後に、出典・尺度の向き・名詞の格関係・重複の意味を確認して接続する必要があります。今回のテスト用スコアは隔離したメモリ内の人工データであり、実際の語学的な頻度や自然さの根拠ではありません。

## 5. 動詞抽出・重複排除・順位

`CollocationAffinity`は動詞・助動詞の品詞だけを採用し、空語、無効な数値（NaN / Infinity）、他品詞を除外します。基本形を前後空白除去・Unicode NFC・小文字へ正規化し、同じ動詞を1件にまとめます。重複は件数やスコアを加算せず最大スコアを採用し、高い順に並べます。実データ未提供のため、この重複ルールは明示的な暫定仕様です。

戦闘では使用技のIDが辞書に存在することを確認し、技の`infinitive`、未設定なら既存の`skillName`を使います。名詞の表示形や生成文を検索せず、防御側キャラクター種類IDと基本形で参照します。活用・複数形・分離動詞の例文生成は従来どおりです。任意の活用形を解析して基本形に戻す新しい形態素解析器は追加していません。

## 6. Nが5未満

動詞抽出・重複排除後のNが0〜1ならすべて通常。Nが2〜4なら単独で最高スコアの1件だけ効果抜群、残りは通常です。最高が同点なら厳密な1位がないため全件通常です。いまひとつは発生しません。

## 7. Nが5以上

`k = floor(N / 5)`とし、上位k件を効果抜群、下位k件をいまひとつ、残りを通常とします。N=5 / 7では各1件、N=10では各2件です。同点境界は次節のルールを適用します。

## 8. 未登録動詞・データ欠落

リストにない動詞、不明な技ID、リストなし・空、動詞なし、対応外言語では通常です。欠落を不利の証拠と見なしません。任意の共起JSONの構文不正も警告と通常判定へフォールバックします。現在は実共起データが未収録のため、実マスターでの相性は通常になります。

## 9. 同点順位

同点グループをID順などで分割しません。グループ全体が上位k件内なら効果抜群、下位k件内ならいまひとつ。境界をまたぐグループは全体を通常にします。すべて同点の場合も通常です。これにより有利／不利の件数が20%より少なくなることがあります。N=2〜4の最高同点はすべて通常です。境界・境界内・全同点をテストしています。

## 10. ダメージと表示への接続

通常ダメージの基礎式、形容詞、副詞、最低1ダメージ、既存の丸めを維持し、性別補正だけを3段階の相性補正へ置き換えました。効果抜群1.5倍、通常1倍、いまひとつ0.75倍を使用します。スコアそのものを掛けません。その後のmüssen2倍・Futur I1.5倍も従来の順序と丸めです。

`BattleActionResult`に判定を保持し、両陣営のログと実ダメージで同じ値を使用します。Futur Iは予約時の相性・丸め済みダメージ・文章を保持し、発動時に共起順位や装備を再計算しません。赤／灰色の既存表現を維持し、通常相性も日英で表示します。

## 11. チュートリアルへの影響

Stage 1〜5の受講、再受講、スキップ、全スキップ、ガイド終了、敗北、勝利報酬、自由戦闘までの機能制限を既存テストとUIで確認しました。`TutorialFeaturePolicy`、ステップロック、敵HP・待機条件、AI、報酬、ステージ定義は変更していません。

従来の性別有利を前提にした固定ダメージの期待値は通常相性へ修正しました。例としてNachtのsein→Feuerは従来14、データ欠落による通常相性では9です。敵側の旧不利補正もなくなります。最弱技だけの連打で負ける受講テストは、習得済み副詞と装備中の強い技を使う操作に修正しました。敵や味方の能力を上げて解決していません。Futur I即死検証は固定の旧ダメージでなく、実際の予約ダメージをテスト用敵HPに設定します。

## 12. 変更ファイル

- `Assets/Resources/Data/dialogues.json`（変更）
- `Assets/Resources/Data/tutorials.json`（変更）
- `Assets/Resources/Data/ui_translations.json`（変更）
- `Assets/Scripts/BattleActionResolver.cs`（変更）
- `Assets/Scripts/BattleManager.cs`（変更）
- `Assets/Scripts/CharacterDetailManager.cs`（変更）
- `Assets/Scripts/CharacterLoadoutStore.cs`（変更）
- `Assets/Scripts/CharacterSelectManager.cs`（変更）
- `Assets/Scripts/CollocationAffinity.cs`（新規）
- `Assets/Scripts/CollocationAffinity.cs.meta`（新規）
- `Assets/Scripts/CraftingUIManager.cs`（変更）
- `Assets/Scripts/DamageCalculator.cs`（変更）
- `Assets/Scripts/DialogueUI.cs`（変更）
- `Assets/Scripts/GameData.cs`（変更）
- `Assets/Scripts/GameDatabase.cs`（変更）
- `Assets/Scripts/SettingsInstaller.cs`（変更）
- `Assets/Scripts/StageRowUI.cs`（変更）
- `Assets/Scripts/StageSelectManager.cs`（変更）
- `Assets/Scripts/TutorialChoiceUI.cs`（変更）
- `Assets/Scripts/TutorialController.cs`（変更）
- `Assets/Scripts/TutorialData.cs`（変更）
- `Assets/Scripts/TutorialFocusUI.cs`（変更）
- `Assets/Tests/EditMode/BattleModifierTests.cs`（変更）
- `Assets/Tests/EditMode/BattleRulesTests.cs`（変更）
- `Assets/Tests/EditMode/CollocationAffinityTests.cs`（新規）
- `Assets/Tests/EditMode/CollocationAffinityTests.cs.meta`（新規）
- `Assets/Tests/EditMode/LoadoutTests.cs`（変更）
- `Assets/Tests/EditMode/TutorialTextTests.cs`（新規）
- `Assets/Tests/EditMode/TutorialTextTests.cs.meta`（新規）
- `Assets/Tests/PlayMode/BattleModifierSceneTests.cs`（変更）
- `Assets/Tests/PlayMode/BattleSceneTests.cs`（変更）
- `Assets/Tests/PlayMode/CharacterInstanceSceneTests.cs`（変更）
- `Assets/Tests/PlayMode/LoadoutSceneTests.cs`（変更）
- `Assets/Tests/PlayMode/Phase121SceneTests.cs`（新規）
- `Assets/Tests/PlayMode/Phase121SceneTests.cs.meta`（新規）
- `Assets/Tests/PlayMode/StageTutorialSceneTests.cs`（変更）
- `Assets/Tests/PlayMode/TitleSceneTests.cs`（変更）
- `Documentation/Phase12.1_チュートリアル文言・技装備UI・共起相性レポート.md`（新規）
- `Tools/UIPlayValidation.cs`（変更）
- `Tools/validate_localization.py`（変更）

既存Scene・Prefab・既存metaは内容不変です。新規スクリプトとテストだけ新しいmetaを追加しています。新規のランタイム型は`BattleAffinity`、`VerbCollocation`、`NounCollocations`、`NounCollocationTable`、`CollocationAffinity`です。セーブ型・versionは変更していません。

## 13. テスト結果

| 検査 | 結果 |
|---|---:|
| EditMode（既存217＋追加38） | 255 / 255成功 |
| PlayMode（既存97＋追加6） | 103 / 103成功 |
| 公開境界 | 8 / 8成功 |
| 日英Localization | 362キー成功（旧355キー不変） |
| Scene / Prefab参照 | 26件成功 |
| 通貨・リセットのRelease / Development / Editor制限 | 既存全検査成功 |

最初の試行で、旧性別補正に依存した期待値、検証用パスの旧Phase番号固定、画面検証で閉じた技パネルを再表示せず押そうとする手順を検出しました。期待値・隔離先チェック・操作手順を修正して再実行しました。失敗した試行のログも検証ディレクトリに保存しています。

新規テストは、N別・品詞混在・重複・未登録・同点・防御側参照・性別非依存・分離動詞基本形・予約スナップショットを含みます。PlayModeでは旧セーブ互換、個体別技編集、言語変更で状態不変、3段階すべての実戦ログとHPを人工データで照合します。

## 14. UI検証

日本語・英語の1920×1080、1280×720、1600×1200、2560×1080で、全体1020枚・49,608件、追加重点検証80枚・2,856件が成功、失敗0です。強調対象・通常操作の押下位置、未習得の無効化、言語別本文とキーの一致、文字の収まり、技変更・破棄、形容詞選択、副詞選択・解除、相性3段階とHPを検証しました。追加の相性画面は人工データであることをファイル名にも明記しています。全体検証後にWebで発見した動的ボタンの英語表示を修正し、その変更箇所を追加重点検証で再確認しました。描画検査と抜き取り目視を併用し、すべての画像を1枚ずつ人間が評価したという意味ではありません。

## 15. Webビルド・ブラウザ

Unity 6000.4.8f1の非Development Webビルドに成功しました。最終ローカルパッケージは`vercel-candidate-release`、release=`75c3684bd64342c738945609`、実行用12ファイル・58,272,614 bytesです。Brotli、MIME、キャッシュ、ファイルハッシュをHTTPで照合し、ソース・実セーブ・設定・運用ファイルへの8パスが404になることを確認しました。既存の`WebPersistentDataBuild`とVercel設定は変更していません。アップロード・Production公開は行っていません。

専用のlocalhost originを空の状態から使用し、10回のIndexedDB保存内容を記録しました。

- 新規開始でNacht個体1体と3,000 Coins。
- 日本語の技一覧をスクロールし、seinからbrennenへ変更・保存。
- 同じoriginのビルドを更新しても、個体IDと装備が維持されること。
- Stage 1の日本語会話・ヒント、自由戦闘でも副詞・müssen・Futur Iが無効なこと、通常技だけでの勝利。
- 初回勝利で400 Coins、素材、チュートリアル完了、Stage 2解放を記録し、残高3,400 Coins。
- 英語設定を保存して最終版へ更新し、TitleのContinueから同じ進行を再開。
- 最終版の再受講画面で「閉じる」、会話で「次へ」が初回から日本語になること。
- 再受講で制限が再適用され、「ガイドを終了」で同じ戦闘の全機能が解放されること。
- ガイド終了後、müssenの18ダメージと使用済み表示、Futur Iの即時0・次ターン14ダメージ、予約による自動勝利。
- 再勝利の報酬は100 Coinsだけで、初回報酬を重複せず残高3,500 Coins。
- 個体ID、技、所持素材、クリア回数2、日本語設定が次の再起動でも維持されること。

Titleでの読み込みとWeb更新は、保存ファイルの本文・更新日時も不変でした。Continue時には既存処理による同一本文の再保存とバックアップ更新があり、進行内容・設定は維持されています。この再保存の挙動は今回変更していません。最終ブラウザのコンソールエラーは0件です。

1280×720で実操作を確認しました。390×844・844×390ではTitle表示、横向きメニュー／確認のスクロールと新規開始、Battleの描画も確認し、縦向きから技を押してダメージを与えられました。ただし縦長のBattleは既存のレイアウトが縮小され、文字とボタンが小さくなります。実機タッチの快適性を保証する検証ではありません。

ブラウザ検証用の保存観察パネル・サーバーは検証ディレクトリにだけ置き、ビルド・公開パッケージ・Unityソースへ含めていません。実コーパスによる有利／不利の照合は、データ未提供のため実施していません。


## 16. 既存データ保護

開始時はmain、`d4dd4cb1506ad5db1d61aaa817591b5522875675`、未コミット差分なしです。開始時の841ファイルのハッシュを保存しました。実進行132ファイル・実設定2ファイルの内容・更新日時・作成日時は開始前と検証後で一致しています。反映前後の照合も完了し、元Editorのコンパイルエラーは0件です。2026-11-08の再開時には`Assets/Resources/Fonts/NotoSansCJKjp UI.asset`に字形の合字・ペア調整情報の追加差分がありました。この既存差分は別途退避し、今回の40ファイルとは別に保持しました。検証用コピーとWebビルドは開始時のフォント資産を使用しており、再開時のフォント差分自体は本変更の検証対象外です。作業用コピーと専用の保存先だけでテストし、対象外の元ファイルは保持しました。

Title、セーブv6、UserSettings version1、初期Nachtと3,000 Coins、個体GUID、ガチャ、合成、素材・報酬、Web永続化、Vercel配信設定の実装・定義は変更していません。旧副詞装備データは互換読み書きに残し、実セーブを変換していません。commit / push / Productionデプロイは実行していません。

## 17. 残る課題・手動確認

**実共起データが未提供です。複数の実名詞で語学的な順位と戦闘表示を照合する完了条件は満たしていません。** 元ファイルの所在が分かり次第、出典・品詞タグ・スコア方向・重複の意味を確認し、種類IDと不定詞へ対応付ける必要があります。装備技配列の順序を共起順位に置き換えたり、人工スコアを実データとして導入してはいません。

Editorでは次を確認してください。

1. 日本語に設定してStage 1〜5を受講。会話・強調ヒント・自由戦闘ヒントが日本語で、ドイツ語は維持されること。
2. Home / StageSelectで表示言語を変えて再受講し、説明が選んだ言語になること。戦闘中に設定入口はなく、実行中の言語更新とHP・進行保持は内部APIを使ったテストで確認済みです。
3. CharacterDetailの技スロットを開き、動詞技だけが表示されること。変更→破棄と変更→保存、同種の別個体、形容詞のschwarzも確認すること。
4. 戦闘で副詞を選択・変更・解除し、旧副詞装備による自動適用がないこと。müssen・Futur I・未習得ロックも確認すること。
5. 実データ未接続の現在は「通常の相性」になること。効果抜群／いまひとつの人工データ検証は実コーパス検証の代わりにならないこと。

Safari・iPhone／Android実機のタッチは未検証です。390×844のBattleでは既存UIの文字・ボタンが小さく、快適なタッチ操作用の配置にはなっていません。マウスで技を押して攻撃できることは確認しました。また、受講中の上部ヒントが戦闘ログの先頭文に重なる箇所があります。いずれも今回レイアウトを作り直して解消したものではなく、今後のUI調整課題です。既知の一部画像・音声不足とUnity Editor内部の`UnityEditor.Search.SearchDatabase`初期化例外はゲーム側の不具合と分けて記録しています。音声素材不足時の単語フォールバックも維持しています。ブラウザの保存拒否・容量不足・書き込み直後の強制終了に対する新しい保証は追加していません。
