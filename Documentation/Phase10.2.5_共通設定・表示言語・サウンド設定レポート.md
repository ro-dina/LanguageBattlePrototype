# Phase 10.2.5 共通設定・表示言語・サウンド設定 実装レポート

## 1. 調査した既存構成

Unity 6000.4.8f1。ビルド対象はGermanyHome、StageSelect、CharacterSelect、CharacterCollection、CharacterDetail、Battle、Result、Gacha、Inventory、Craftingの10 Sceneです。SampleSceneは無効でUIがありません。既存Scene、Prefab、Inspector参照、TextMeshPro、Tests、セーブv6、通貨HUD、会話・チュートリアルを調査しました。

固定文字列はScene/PrefabのTMPと、画面管理クラス・会話/ステージ/チュートリアルJSONに分散していました。Localizationパッケージや共通翻訳機構はありませんでした。フォントは主にLiberationSans SDFで、日本語の収録が不足していました。

学習発音はAttackAudioPlaybackが文章音声→単語音声の順にフォールバックします。BattleManagerは同じ既存AudioSourceで効果音も再生していました。音量管理・AudioMixer・既存のBGM曲/再生システムはありませんでした。

## 2. 採用したLocalization方式と理由

日本語/英語の小規模な固定UIが中心のため、JSONテーブルと軽量な独自機構を採用しました。Unity Localizationは追加言語・Addressables・翻訳アセット管理が拡大した段階で有効ですが、今回のためのパッケージ導入と既存参照の移行を避けられます。Packagesは変更していません。

- 安定したキーで日英を管理する`ui_translations.json`。
- `Localization.Text(key, args)`によるパラメーター付き表示。
- 既存Sceneの固定ラベル/合成された状態表示は、同じテーブルの英語原文からキーに解決する互換表示層を使用。未知の語を自動翻訳する処理はありません。
- 動的なキャラクター名・素材・報酬・会話は、キーとパラメーター、または表示専用の再整形関数を利用。抽選・保存・画面再生成を言語変更で再実行しません。
- `LocalizedText`は元の表示内容を保持し、実行中に切り替えます。入力中の検索語やドイツ語だけの欄は翻訳から除外します。
- 欠落した日本語は英語へフォールバック。未知キーは診断用一覧へ記録し、画面にはキーを露出せず「Unavailable/表示できません」を表示。重複キー・空翻訳・パラメーターの不一致を検出できます。
- HP/ATK/DEF/SPD/Lv.には共通表記の翻訳キーを用意し、内部パラメーター名と分離しました。

表示言語`ja/en`、学習言語`de`、音声言語`de`を区別します。「Name」という実在するドイツ語キャラクターとUIの「Name/名前」が混同しない表示境界も保護しました。

## 3. 新規作成・変更ファイル

- `Assets/Resources/Data/ui_translations.json`
- `Assets/Resources/Fonts/NotoSansCJKjp UI.asset`
- `Assets/Resources/Fonts/NotoSansCJKjp-LICENSE.txt`
- `Assets/Resources/Fonts/NotoSansCJKjp-Regular.otf`
- `Assets/Resources/UI/SettingsLauncher.prefab`
- `Assets/Resources/UI/SettingsUI.prefab`
- `Assets/Scripts/AttackAudioPlayback.cs`
- `Assets/Scripts/AudioChannelVolume.cs`
- `Assets/Scripts/BattleManager.cs`
- `Assets/Scripts/CharacterCollectionManager.cs`
- `Assets/Scripts/CharacterDetailManager.cs`
- `Assets/Scripts/CharacterSelectManager.cs`
- `Assets/Scripts/CraftingUIManager.cs`
- `Assets/Scripts/CurrencyHUD.cs`
- `Assets/Scripts/DialogueUI.cs`
- `Assets/Scripts/GachaManager.cs`
- `Assets/Scripts/GameAudio.cs`
- `Assets/Scripts/GermanyHomeManager.cs`
- `Assets/Scripts/InventoryUIManager.cs`
- `Assets/Scripts/Localization.cs`
- `Assets/Scripts/LocalizationUI.cs`
- `Assets/Scripts/LocalizedText.cs`
- `Assets/Scripts/ResultManager.cs`
- `Assets/Scripts/SettingsInstaller.cs`
- `Assets/Scripts/SettingsUI.cs`
- `Assets/Scripts/StageRowUI.cs`
- `Assets/Scripts/StageSelectManager.cs`
- `Assets/Scripts/UserSettings.cs`
- `Assets/Tests/EditMode/LanguageBattle.EditModeTests.asmdef`
- `Assets/Tests/EditMode/SettingsLocalizationTests.cs`
- `Assets/Tests/PlayMode/BattleRewardSceneTests.cs`
- `Assets/Tests/PlayMode/SettingsSceneTests.cs`
- `Assets/Tests/PlayMode/StageTutorialSceneTests.cs`
- `Assets/Tests/PlayMode/TestSaveIsolation.cs`
- `Documentation/Phase10.2.5_共通設定・表示言語・サウンド設定レポート.md`
- `Tools/BuildSettingsUI.cs`
- `Tools/UIPlayValidation.cs`
- `Tools/validate_localization.py`

計55ファイル（新規meta 17点を含む）。

既存Scene/Prefab自体の変更はありません。新規のSettingsUI/SettingsLauncherをResourcesから共通生成します。既存のmeta、GUID、fileID、Inspectorイベントは維持しています。

## 4. 日本語・英語対応の画面

| Scene | 対応する固定UI | 設定入口 |
|---|---|---|
| GermanyHome | 全メニュー、設定 | 既存Settings＋右上歯車 |
| StageSelect | ステージ番号/状態/報酬/挑戦、受講選択、スキップ設定 | 既存設定ボタン＋歯車 |
| CharacterSelect | 選択案内、個体数/番号、ステータス説明、詳細/戦闘 | 歯車 |
| CharacterCollection | 検索、性別/並べ替えの表示、発見/所有状態、詳細 | 歯車 |
| CharacterDetail | 個体情報、装備、変更/保存/破棄/確認、未所有表示 | 歯車 |
| Inventory | 見出し、戻る | 歯車 |
| Gacha | 単発/10連、費用/残高、不足/エラー、結果の固定文言 | 歯車 |
| Crafting | 選択/取消、費用、個体消費の確認、結果/エラー | 歯車 |
| Battle | 任意修飾/予約/使用済み、戦闘ログの説明、操作ヒント、Technik会話 | 入口なし、直接呼出しも拒否 |
| Result | 勝敗、報酬/素材/初回報酬/クリア/受講完了、戻る | 歯車 |

設定は一般・サウンド・データの3タブです。一般には表示言語、学習/音声言語の説明、既存のチュートリアル自動案内設定への入口があります。データタブは追加予定の説明のみで、リセットボタンやリセット処理はありません。

検索・フィルタは表示された日本語を内部条件に使用せず、従来の選択値を維持します。図鑑の並べ替えや性別による抽出は日本語でも動きます。

## 5. 翻訳しない内容・対象外

ドイツ語のキャラクター/技/装備/副詞名、冠詞・活用、実際の素材文字列、合成入力、生成文章、発音、辞書マスターを維持しています。CoinsとHP/ATK/DEF/SPD/Lv.は共通表記です。

開発者向けConsoleのログ、内部検証エラー、未使用サンプル/RecoveryのUIは翻訳対象外です。ユーザー向けの処理失敗は固定の翻訳済み案内を表示します。ステージ2〜5の詳細チュートリアルは追加せず、「操作ガイドは今後追加予定」という案内を翻訳しました。

今回対象の主要画面で、翻訳が必要な固定UIの未対応残りはありません。`日本語`と`English`は言語名として常に表示します。未使用の任意NPC会話を今後追加する場合は、会話IDに対応する日英キーも追加してください。

## 6. 設定モーダルとScene管理

共通のSettingsLauncherは通貨HUDと隣接する右上に表示します。設定Canvasは270、通貨HUDは200、歯車は210、既存受講選択は260、会話は280、保存再試行は300です。

全画面の背景遮断と、背後CanvasのCanvasGroupによる操作無効化で、ポインターとキーボードから背後のボタンへ移動できないようにします。選択中の個体、装備下書き、合成入力、スクロールなどを再生成しません。閉じると元の入力状態/選択を復元します。多重表示は拒否し、Scene終了時に設定と試聴音声を破棄します。会話中や戦闘中は設定を開けません。

## 7. 音量管理・ボイス試聴

現在の音声数と既存処理に合わせ、AudioMixerの新設に代えて、`GameAudio`と`AudioChannelVolume`で区分と音量を一元化しました。実音量は「元の音量×マスター×BGM/効果音/ボイス」です。0%は0になります。

同時に鳴る効果音と発音が互いの音量を変えないよう、既存AudioSourceの子に区分別Sourceを作成します。AttackAudioPlaybackの音声選択・待ち時間・文章から単語へのフォールバックを維持し、再生経路だけをボイス区分にしました。既存slash等の効果音は効果音区分です。

試聴は既存のFeuerドイツ語単語音声を利用します。再試聴では前の音声を止め、設定を閉じた場合やScene遷移でも止めます。不足音声はボタン無効化/案内で扱い、クラッシュしません。`GameAudio.CharacterClip`は将来の詳細画面からも利用できます。

既存BGM曲はないためBGMを新規作曲/追加していません。BGMの音量経路はテスト音源を通して検証しました。今後の音源はGameAudio経由、またはAudioChannelVolumeを付けて区分を指定してください。

## 8. 設定の保存

進行セーブとは別の`Application.persistentDataPath/LanguageBattleSettings/settings.json`に設定version1を保存します。UI言語と4種類の音量だけを持ち、Coins・所有個体・装備・素材・ステージ・チュートリアル記録は含みません。

初回はOSが日本語なら日本語、それ以外は英語です。既存設定があればOSより優先します。スライダーは0〜100%を表示し、変更を即時保存・反映します。保存できない場合は前の設定を維持し、再試行案内を出します。

書き込みは一時ファイルへ出力→検証→置換し、バックアップを保持します。読めない本体は一時ファイル/バックアップから復旧します。変更時も破損元は別名で保持します。新しいバージョンが本体/一時/バックアップのどこかにあれば、上書きを防ぎます。将来の通常リセットで進行フォルダだけを初期化すれば、表示言語・音量は維持できます。

## 9. セーブ互換性・既存データの保護

ゲーム進行のSaveDataはv6を維持し、今回のための移行処理を追加していません。既存のv1〜v5からの移行も維持します。価格、確率、レシピ、基本能力、戦闘計算、Futur I、müssen、文字列ドロップ、報酬、進行条件は変更していません。

作業用コピーと専用の進行/設定ディレクトリで検証しました。実進行プロフィール114ファイルの内容ハッシュ・作成日時・更新日時は作業前後で一致しています。実設定フォルダも未変更です。元ソース726ファイルとGit状態を作業前の記録と照合し、反映直前まで一切変更していません。旧マスター/価格/確率/レシピ/音声/画像/Scene/Prefab/metaと保護対象ロジックを個別に照合しました。

## 10. Edit Mode / Play Mode

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode（既存164＋追加16） | 180 | 0 |
| Play Mode（既存54＋追加8） | 62 | 0 |
| 合計 | 242 | 0 |

[Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/edit-final.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/play-final.xml)。

新規検証は、初期言語、切り替え/再起動/遷移、翻訳の欠落/重複/パラメーター/フォールバック、全翻訳文字のフォント収録、ドイツ語マスター/生成文章/進行不変、4音量/ミュート/区分の独立、試聴/欠落音声、設定保存/破損/新形式保護/失敗時の復元を含みます。

実Sceneでは非戦闘9画面、背後操作の遮断、閉じる/多重表示/Scene終了、装備下書きの維持、データタブ、日本語の図鑑フィルタ、日英両方でTechnikの同じ操作ロック→実攻撃→説明→勝利→初回400/再戦100を検証しました。言語変更時に会話ID/カーソルが維持されることも確認しました。ガチャの動的結果とFeuer練習戦の報酬も日英で切り替え、再抽選・個体ID変更・報酬再付与が起きないことを検証しました。

既存テストのうち、Home/StageSelectから旧チュートリアル設定へ進む操作は、新しい一般タブ経由に更新しました。結果画面の「全ボタンは下部」という旧検査は、追加された上部歯車と、従来の下部遷移ボタンを区別するよう更新しました。

## 11. UI確認

1920×1080、1280×720、1600×1200、2560×1080で392枚を実描画しました。25,750項目成功、失敗0件です。既存検証に、両言語の主要9画面、設定3タブ、受講選択、Technikの導入/技説明/ハイライト/攻撃後説明を追加しました。文字切れ・日本語グリフ不足の警告は0件でした。

[画面検査結果](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/validation.txt)、[表示文字列の調査](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/labels.txt)。共通Scene/Prefabの参照監査も23点成功しました。翻訳284項目は`Tools/validate_localization.py`でキー・言語・パラメーター・会話原文を検査できます。

日本語は再配布可能なNoto Sans CJK JPを同梱しました。既存LiberationSansは置換せず、実行時のフォールバックとして使用します。設定UI自身はNotoを使用します。Dynamic TMPによって追加文字を必要時に生成し、全翻訳文とä/ö/ü/ßの表示を検査しました。ライセンス原文と出典を同梱しています。

フォント出典：[Noto公式フォント](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf)、[SIL Open Font License](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)。

- [日本語の一般設定](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/ja-Settings-General-1920x1080.png)
- [英語の一般設定](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/en-Settings-General-1920x1080.png)
- [サウンド・1280×720](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/ja-Settings-Sound-1280x720.png)
- [日本語のTechnik会話・1600×1200](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/ja-Technik-Verbs-1600x1200.png)
- [日本語のステージ一覧・2560×1080](/Users/isao/Documents/ChatGPT/ゲーム/phase1025-validation/ui/ja-StageSelect-2560x1080.png)

未解決のテスト失敗はありません。UI検証の警告13種類は、既存の不足音声8種類、既存の不足画像2種類、故意に再現した保存失敗・ガイド中断2種類・Scene遷移による会話中断1種類です。音声は既存フォールバック、画像は既存プレースホルダーで処理します。Unity内部SearchDatabase診断は0件でした。

今回の確認はEditor内です。配布用プレイヤービルド、実機の出力音量・音質の聴感確認、未追加BGM曲、Stage 2〜5の詳細ガイド、新たな学習対象言語、ゲームデータのリセットは今回の完了範囲に含みません。

## 12. Unity Editorでの手動確認

1. Homeの設定または右上歯車を開き、「一般」で日本語/Englishを切り替える。設定自身と背後のメニューの更新、閉じて戻る操作を確認。
2. サウンドで4音量を動かし、マスター/ボイスを0%にした試聴と連打を確認。実際の聞こえ方はEditorのミュート設定や出力機器にも依存するため耳でも確認。
3. Playを停止して再開し、言語・音量が維持されることを確認。新規ゲーム進行へのリセット操作は今回ありません。
4. キャラクター詳細で装備を未保存のまま設定を開閉し、下書きを維持して保存/破棄できることを確認。
5. ガチャ、合成、図鑑、インベントリ、ステージ選択、結果の歯車を確認。消費確認中も背景操作が遮断されることを確認。
6. Stage 1の受講/スキップ/再受講を日英で確認。会話中の他の技・修飾・遷移が禁止され、戦闘中に設定が開けないことを確認。
7. Technik初回勝利の400 CoinsとStage 2解放、再戦100 Coinsを確認。ドイツ語の文章・発音・素材内容が翻訳されていないことを確認。
8. データタブには追加予定の説明だけがあり、実行できるリセット機能がないことを確認。
