# Phase 9.2：単語キャラクター統合・辞書合成・報酬移行

2026-10-31 / Unity 6000.4.8f1

マスター登録語をガチャ・合成で直接キャラクター個体として獲得するよう変更しました。合成では、選んだ文字列素材と特定個体を順番どおり連結して辞書と照合し、消費と生成をまとめて確定します。旧単語アイテムはversion 3への安全な移行対象です。実プレイヤーのセーブをテストには使用しておらず、今回そのファイルを移行・書換えしていません。

## 1. 変更・追加ファイル

- `Assets/Resources/Data/characters.json`
- `Assets/Scenes/Crafting.unity`
- `Assets/Scripts/BattleManager.cs`
- `Assets/Scripts/CharacterDictionary.cs`（新規）
- `Assets/Scripts/CharacterInstances.cs`
- `Assets/Scripts/CharacterSelectManager.cs`
- `Assets/Scripts/CraftingManager.cs`
- `Assets/Scripts/CraftingSelection.cs`（新規）
- `Assets/Scripts/CraftingUIManager.cs`
- `Assets/Scripts/GachaManager.cs`
- `Assets/Scripts/GameDatabase.cs`
- `Assets/Scripts/InventoryManager.cs`
- `Assets/Scripts/PlayerSaveService.cs`
- `Assets/Scripts/PlayerStateTransactions.cs`（新規）
- `Assets/Scripts/RewardGrantService.cs`（新規）
- `Assets/Scripts/SaveData.cs`
- `Assets/Scripts/SaveDataMigration.cs`
- `Assets/Scripts/SaveFileStore.cs`
- `Assets/Tests/EditMode/CharacterInstanceTests.cs`
- `Assets/Tests/EditMode/DictionaryCraftingTests.cs`（新規）
- `Assets/Tests/EditMode/GachaContentTests.cs`
- `Assets/Tests/EditMode/SaveMigrationTests.cs`
- `Assets/Tests/EditMode/SaveTests.cs`
- `Assets/Tests/EditMode/WordItemMigrationTests.cs`（新規）
- `Assets/Tests/PlayMode/CharacterInstanceSceneTests.cs`
- `Assets/Tests/PlayMode/SaveSceneTests.cs`
- `Tools/BuildPhase92UI.cs`（新規）
- `Tools/UIPlayValidation.cs`
- `Documentation/Phase9.2_辞書合成・単語報酬移行レポート.md`（本書、新規）

上記に新規C#アセット6件のmetaを加え、計35ファイルです。既存metaとPrefabは変更していません。Phase 9.1からの未コミット変更25ファイルも保護しています。

新規の主な型はCharacterDictionary、GermanWordMatching、WordCharacterRewards、CraftingElement（CraftingElementKind）、RewardGrantService、PlayerStateTransactions、WordItemConversionです。新規テストはDictionaryCraftingTestsとWordItemMigrationTestsです。

## 2. 調査結果とキャラクター辞書

作業前のマスターは203種類、同一言語内での表記重複は0件でした。種類IDは小文字・ASCII転写を含み、表示名と必ずしも一致しません。例はnacht→Nacht、wasser→Wasser、maerz→März、strasse→Straßeです。

Pistoleは未登録だったため、ユーザー確認に基づきid=pistoleを新規登録しました。Wasserpistoleの初期能力（HP22、ATK7、DEF3、SPD5、sein/haben/werden）と画像参照を暫定利用し、計204種類になりました。元の203種類の定義は変更していません。

CharacterDictionaryはcharacterNameと言語を照合します。GermanWordMatchingでNFC正規化と不変の小文字化を行い、n+a+c+h+tでもNachtに一致します。ä/ae、ö/oe、ü/ue、ß/ssは同一視しません。文字順を保ち、アナグラム検索は行いません。将来の言語ルールはドイツ語用の正規化から分離できます。

同じ言語・正規化後の表記に複数候補がある場合、Findは不一致を返します。任意の先頭候補を選びません。実マスターに曖昧性はありませんが、テストで安全な不一致を確認しています。

調査証跡：[元マスターの全対応表](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/content-audit.json)。

## 3. 固定レシピからの変更

従来のcrafting_recipes.jsonには、単語素材2つのWasserpistole用レシピと、11要素の文字・chunkレシピの2件があります。旧UIは選択したitemIdがレシピinputsの順序と数量に一致することを要求し、CraftingManagerが素材を消費した後に結果を付与していました。

新UIは固定レシピを検索しません。各要素の文字列表記を順番に連結し、マスターにある単語であれば合成候補にします。Wasser個体＋Pistole個体、f+e+u+e+r、Wasser個体＋p+i+s+t+o+l+e、従来の11要素でWasserpistoleを作れることを検証しました。

既存レシピファイル自体は変更していません。GetRecipe/GetAllRecipes/Craft(recipeId)などの旧APIは互換用に残し、登録されたレシピの入力を新しい辞書合成へ接続します。ただし、旧APIに個体指定がない状態で同種の候補が複数あれば、消費個体を推測せず失敗させます。新UIは常に実instanceIdを明示します。

## 4. 個体を合成素材として扱う方法

CraftingElementは文字列素材のitemIdと、所有個体のinstanceIdを別の種類として保持します。キャラクター要素の表記は、その個体の種類から取得したマスターのcharacterNameです。IDや表示番号を連結しません。suffixの表示用ハイフンは合成時の文字列から除きます。

左一覧は[Material]と[Character]を区別し、キャラクターは種類ごとに1行と所有数を表示します。種類を押すと個体選択ダイアログを開き、左右で切り替えて個体番号・形容詞・冠詞・技と副詞を確認し、Add individualで追加します。同じ個体の二重選択を拒否し、別IDの同種個体は選択できます。

右側には順序どおりの選択要素、完成文字列、辞書照合結果を表示します。一致した完成語はマスターの正しい表記で表示します。要素数の固定スロット制限はありません。Undo lastと既存Clearで取り消せます。不一致または所持状態が無効ならCraftを無効にします。

個体を含む合成では、消費する個体番号と装備、消費される素材、生成結果を確認してからCraft & consumeを押します。確認前・Cancel時には消費しません。長い確認内容はスクロールできます。確定直前にも所有状態を再検証します。

## 5. 消費と生成の安全性

PlayerStateTransactionsは現在状態をコピーし、コピー上で以下を準備します。

1. 素材数量、入力の有効性、同じ個体の重複、現在の所有と消費可否を検査する。
2. コピーから素材・指定個体・その個体の装備だけを取り除く。
3. 結果の新個体IDを発行し、発見記録へ登録する。新個体には装備上書きを付けず、マスターの初期装備を使う。
4. セーブ全体の参照関係と、現在状態が準備中に変化していないことを検証する。
5. PlayerSaveServiceがある場合、候補状態のファイル保存を先に成功させ、その後メモリ状態をまとめて置換し、1回の通知を出す。

したがって、通知をまとめるだけの処理ではありません。入力が無効、個体が削除済み、保存失敗などの場合は、候補を破棄して元の所持・装備・発見記録・選択を保持します。確定処理中の再入も拒否します。保存失敗ではUIに「nothing consumed」と理由を表示します。

処理はUnityのメインスレッド上で同期的に行います。セーブ成功後にプロセスが終了しても次回は確定済み候補をロードし、保存中に終了した場合は既存の原子的ファイル置換とバックアップで復旧します。

CharacterInstances.CanConsumeを消費制約の窓口とし、戦闘中の個体を拒否します。現在の進行データに必須個体・育成ロックの指定はありません。成功した合成では必ず別のプレイアブル個体が1体残るため、最後の所有個体を単に消滅させる処理にはなりません。将来の個体ロックはこの窓口へ追加できます。今回、ロックUI・分解・強化は追加していません。

## 6. ガチャからの直接個体生成

従来は抽選後にInventoryManager.AddItemを呼び、word_nacht、word_wasser、word_pistoleも単語アイテムとして追加していました。

今はGachaManager→RewardGrantService→辞書照合→新個体または素材、の順です。itemType=wordかつマスターの表記・言語へ一意に対応するものだけを個体化します。意図的に別用途のchunk/suffixなどは変換しません。非対応語は素材のまま、曖昧な単語報酬は安全に拒否します。

単発と10連を同じ報酬経路に統一し、10連は10件をまとめた候補状態として保存・反映します。同じキャラクターが3回出れば異なるIDの3個体を追加します。結果画面にはCharacter obtained!と表示し、UUIDは表示しません。

ガチャテーブル・確率は変更していません。

| 枠 | 合計 | 報酬と1項目の確率 |
|---|---:|---|
| N | 60% | 30文字、各2%の素材 |
| R | 25% | sch・ss・erは素材、Wasser・Pistoleは個体、各5% |
| SR | 12% | -ungの素材、12% |
| SSR | 3% | Nachtの個体、3% |

既存InventoryManager.AddItemに旧単語IDが渡された場合も、互換経路としてRewardGrantServiceへ委譲して個体化します。旧単語アイテムを通常インベントリへ再追加しません。RestoreStateとversion 3の保存検証でも、個体化対象語の在庫残存を拒否します。新規ゲームのNacht 1体、初期所持品と進行は維持しています。

## 7. 移行対象と検証用データでの変換結果

対応は特定IDのハードコードではなく、旧マスターのitemType、displayName、languageとキャラクター辞書から決まります。今回の対象は次の3件です。

| 旧アイテムID | 表記 | 対応種類ID | 検証前 | 検証後の追加個体 |
|---|---|---|---:|---:|
| word_nacht | Nacht | nacht | 26 | 26 |
| word_wasser | Wasser | wasser | 37 | 37 |
| word_pistole | Pistole | pistole（今回登録） | 42 | 42 |

検証用の既存Nacht 1体を保持し、結果はNacht 27体、Wasser 37体、Pistole 42体、合計106体です。旧単語アイテム3件は0個になり、letter_aの54個、既存NachtのID・装備、発見記録とステージ進行を維持しました。

これは隔離したテストデータの結果です。実プレイヤーの現在数量をこの表の値で上書きしたり、検証のため実データを変換したりはしていません。

## 8. version 3と移行・復旧

version 2のinventory、characterInstances、characterEquipment、characterCollection、stageProgressを維持し、version=3とconvertedWordItems（旧アイテムID・対応種類ID・変換数量の記録）を追加しました。移行記録は通常のオートセーブ後も保持します。

version 2をロードする際は旧全セクションを検証し、コピー上で数量分の新個体を作成し、対象の旧在庫だけを取り除きます。既存個体IDと装備には触れません。version 1も、従来の種類ID→個体ID移行を経由してversion 3まで連結します。

元のバイト列をsave.json.v2.bakまたは.v1.bakへFlush後に保護し、結果の検証・保存が成功した時だけメインを置換します。保護バックアップの衝突時は別名を作り、既存ファイルを上書きしません。通常の.bak、破損時のrecoveryファイル、v1/v2保護バックアップからの復旧も維持しています。

追加IDは旧ソースとアイテムID・出現番号から決定的に生成します。移行途中で終了し、同じ旧ソースから再試行しても同じIDです。version 3保存後は移行を繰り返しません。不正な数量・参照・曖昧性・バックアップ失敗では元を保持し、有効な復旧元もない場合は保存を停止します。未対応の将来バージョン4以降は保護します。

既存の4 MB上限を超える移行結果は書き込まず、元を残して停止します。巨大数量は新個体を大量確保する前に上限を検査します。破損したversion 2本体でも有効なバックアップがあれば、元本体を保持したままrecovery先へ復旧できます。

実セーブの移行は、反映後の次回ゲーム起動時にバックアップを作って行われます。

## 9. 装備・図鑑・戦闘への影響

装備は引き続き実instanceIdがキーです。合成で消費する個体の装備だけを取り除き、同種の別個体を保持します。発見記録は消費後も残し、所有0になったPistoleが「発見済み・未所有」になることも検証しました。

Phase 9.1の種類別一覧・左右切替・個体番号、Save/Confirm/Discard/Cancel、Futur I/müssenのトグルは維持しています。ダメージ計算、副詞競合、予約攻撃、助動詞効果、文章生成、音声処理、勝敗、SPDと行動順は変更していません。

Wasser・Wasserpistoleの画像ファイルは元から未配置です。Pistoleの画像参照もWasserpistoleと共通ですが、表示できる画像はありません。未配置の種類を選んだ際は画像を非表示にして名称と能力値で識別し、戦闘でも別キャラクターの初期画像を誤表示せずプレイできるようにしました。既存画像のある種類は従来表示のままです。

## 10. テスト結果

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode | 104 | 0 |
| Play Mode | 34 | 0 |
| 合計 | 138 | 0 |

既存全テストを実行し、仕様変更に伴う旧「単語もすべてInventoryへ追加」の期待値を、素材＋所有個体の合計とID保持の確認へ更新しました。既存のテスト範囲は削除していません。

追加確認は、表記照合・Unicode・曖昧性・順序、文字のみ/個体のみ/混在/11要素/繰返し合成、重複・削除・不足・戦闘ロック・保存失敗・再入時の保護、個体の装備と発見記録、重複ガチャ、Wasser/Pistoleでの実戦闘、v1/v2移行・再試行・数量・装備・記録保持・バックアップ失敗・破損復旧です。

実行証跡：[Edit Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/edit-final.xml)、[Play Mode](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/play-verified.xml)。

## 11. UI検証

指定の1920×1080、1280×720、1600×1200、2560×1080で104枚を実描画し、5,420項目成功、失敗0件でした。Gacha、Inventory、Crafting、CharacterSelect、CharacterDetail、CharacterCollection、Battleと関連画面を確認しています。

Nacht 27・Wasser 37・Pistole 42体を初期配置し、その後のガチャで個体が増える状態を使用しました。種類別一覧、個体の端から端への循環、個体別装備表示、個体選択ダイアログ、辞書候補、消費確認・Cancel・確定後一覧更新を確認しました。各解像度でダイアログと主要ボタンへポインターが届きます。

15 Scene/PrefabのGUID・fileID・参照・既存Inspectorイベント検査も成功しています。変更SceneはCraftingだけで、既存104ブロックを維持し、新規70ブロックを追加しました。

[UI結果](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/ui/validation.txt) / [個体選択](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/ui/Crafting-individual-picker-1280x720.png) / [消費確認](/Users/isao/Documents/ChatGPT/ゲーム/phase92-validation/ui/Crafting-consumption-confirm-1600x1200.png)。

## 12. 残っている問題

機能検証の未解決失敗はありません。画像未配置の種類には絵が表示されません。Pistoleの能力・画像参照は暫定設定です。新しい画像を追加したり既存画像を変更したりはしていません。今後の素材追加に合わせて差し替えてください。

音声素材の不足も残り、文章音声→単語音声の既存フォールバックを維持しています。UI検証では既存音声不足の警告が8種類、Unity内部SearchDatabaseの診断は0件でした。

旧レシピAPIは複数個体から消費対象を推測しません。今後のコードはCraftingElementで明示的な個体IDを渡してください。4 MBを超える大規模移行は自動で切り捨てず停止するため、その場合は保存容量設計の見直しが必要です。

文字列ドロップ、コイン、分解、強化、動的な文字列素材、SPDによる行動順変更は実装していません。分解・強化を追加する際も、種類ID・個体ID・表記を区別し、CanConsumeと準備済み状態の確定経路を利用できます。

## 13. Unity Editorでの手動確認

1. 再インポート完了後にPlayする。旧v2があればバックアップを作り、v3へ移行する。初期化や手動のセーブ削除は不要。
2. GachaでNacht・Wasser・Pistoleが当たるとCharacter obtained!と表示され、CharacterSelectの所有数へ追加される。Inventoryに旧word_アイテムとして増えない。
3. CharacterSelectで種類を選び、左右で個体を切り替え、Detailsの装備と戦闘への反映を見る。画像未配置の種類は絵が出ない。
4. Craftingの[Character] Wasserを押し、個体番号と装備を見てAdd individual。Pistoleも追加し、Wasserpistoleの候補を確認する。
5. Craftを押し、消費対象を確認してCancelした場合に残ることを確認する。再度Craft & consumeで対象個体だけが消え、別のWasserと新しいWasserpistoleが残ることを見る。
6. 文字だけのf+e+u+e+r、文字とキャラクターの混在、Undo last、Clear、No matching character時のCraft無効も確認する。

実装と全テストは作業用コピーで実施しました。元Editorの再生停止をユーザー確認したうえで反映しました。実プロフィール全ファイルのハッシュ・作成日時・更新日時を作業前後で照合し、実セーブを変更していないことを確認しました。
