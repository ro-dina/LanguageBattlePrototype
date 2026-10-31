# Phase 9：キャラクター個体管理・複数所有・セーブ移行

2026-10-30 / LanguageBattlePrototype / Unity 6000.4.8f1

キャラクター種類と所有個体を分離し、同種複数所有、個体別装備、個体選択、図鑑の発見記録、繰り返し合成、セーブversion 2とversion 1からの移行を実装しました。文字ドロップ、コイン、分解・強化のゲーム処理、ステージ進行ルールは追加していません。

検証は `/private/tmp/LanguageBattleFoundation` の作業用コピーで実行しました。起動中の元Unity Editorでテストを実行したり、実プレイヤーのセーブをテスト入力に使用したりしていません。

## 1. 調査結果と変更ファイル

開始時のGit差分は未追跡の `.DS_Store` のみでした。元Editorは起動中でした。最初のプロセス確認は権限制限のため不正確だったので、権限のあるプロセス名確認で再確認しています。元Editorの停止・再生操作は行っていません。

以前はOwnedCharactersが種類IDの集合、CharacterLoadoutStoreが種類IDをキーとする装備辞書でした。現在は以下のように用途を分けています。

| 用途 | 使用するID |
|---|---|
| GameDatabase／CharacterData、レシピの生成先、敵マスター、図鑑の発見記録 | 従来の種類ID（`nacht`、`feuer`などを維持） |
| 所有一覧、選択中のプレイヤー、詳細の編集対象、確定装備、戦闘参加、削除 | 個体ID |
| OwnedCharactersの旧API | 種類単位の互換窓口。新しい個体生成にはCharacterInstances.Createを使用 |

変更した既存ファイル：

| ファイル | 内容 |
|---|---|
| [OwnedCharacters.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/OwnedCharacters.cs) | 種類単位の互換窓口。旧データ型を保持 |
| [CharacterLoadoutStore.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterLoadoutStore.cs) | 装備のキーを個体IDへ変更、削除時の整理 |
| [BattleManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/BattleManager.cs) | GameManagerの個体選択、戦闘開始時の所有・装備確認、戦闘中削除の防止 |
| [CharacterSelectManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterSelectManager.cs) | 所有個体ごとの一覧、短い識別表示、所有数、選択色 |
| [CharacterCollectionManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterCollectionManager.cs) | 種類ごとの所有数・発見状態と、詳細で扱う個体の明示 |
| [CharacterDetailManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterDetailManager.cs) | 開いた個体に編集対象を固定、個体識別表示、未所有時の閲覧専用制御 |
| [CraftingManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CraftingManager.cs) | 既所有種類でも毎回新しい個体を生成、結果の事前検証 |
| [CraftingUIManager.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CraftingUIManager.cs) | 合成後の種類別所有数を表示 |
| [SaveData.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/SaveData.cs) | version 2、新規ゲーム初期値、個体・装備・発見記録の整合性検証 |
| [SaveFileStore.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/SaveFileStore.cs) | 移行前退避、原子的な書き込み、再実行・バックアップ復旧 |
| [PlayerSaveService.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/PlayerSaveService.cs) | version 2の状態保存・復元、選択と戦闘参加個体の整合性確認 |
| [LoadoutTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/LoadoutTests.cs)、[SaveTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/SaveTests.cs) | 個体APIとversion 2へ既存の期待値・準備処理を更新 |
| [LoadoutSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/LoadoutSceneTests.cs)、[SaveSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/SaveSceneTests.cs)、[OwnershipSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/OwnershipSceneTests.cs)、[BattleSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/BattleSceneTests.cs)、[BattleModifierSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/BattleModifierSceneTests.cs) | 既存の動作検証を個体管理へ接続。準備時に前の戦闘Sceneを終了 |
| [UIPlayValidation.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Tools/UIPlayValidation.cs) | 複数個体・装備独立性・図鑑3状態・繰り返し合成の描画と操作確認 |

追加ファイルは次項のRuntime 3点、テスト3点、それぞれのUnity生成 `.meta` 6点と、本レポートです。合計32ファイル（既存19、新規13）です。Scene・Prefab・既存meta・asmdefは変更していません。

## 2. 追加したクラス

- [CharacterInstances.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterInstances.cs)：`CharacterInstance`、`CharacterInstanceState`、`CharacterInstances`。
- [CharacterCollection.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/CharacterCollection.cs)：`CharacterCollectionState`、`CharacterCollection`、状態列挙型 `CharacterCollectionStatus`。
- [SaveDataMigration.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Scripts/SaveDataMigration.cs)：`LegacySaveData`、`SaveDataMigration`と内部のバージョン読取型。
- [CharacterInstanceTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/CharacterInstanceTests.cs)、[SaveMigrationTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/EditMode/SaveMigrationTests.cs)、[CharacterInstanceSceneTests.cs](/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype/Assets/Tests/PlayMode/CharacterInstanceSceneTests.cs)。

強化値・レベル・限界突破などの未確定パラメータは追加していません。

## 3. 個体IDの生成・管理方式

通常の入手は `character-` + `Guid.NewGuid().ToString("N")` を使用します。CharacterInstanceはinstanceIdとcharacterIdだけを保存し、CharacterDataの能力値などは複製して永続化しません。

CharacterInstancesにはCreate、Get、GetByCharacterId、GetAll、Owns、Remove、Countを用意しています。取得結果は内部状態から切り離したコピーです。取得したオブジェクトを書き換えて所有情報を変更することはできません。

Removeは対象個体の装備だけを削除し、選択中なら別の所有個体へ選択を移します。0体なら未選択になります。発見記録と同種の別個体は残ります。削除済みのIDを指定しても、別の同種個体で戦闘を代行させません。

BattleManagerが使用中の個体には戦闘中の保護を掛け、Removeをfalseで拒否します。Scene終了時に解除します。ロードについても、進行中の戦闘個体が消える状態の復元は拒否し、先に所持品などの一部だけを変更しないようにしています。

## 4. 個体別装備

CharacterLoadoutにinstanceIdを加え、Overridesのキーを個体IDにしました。種類IDは対応の検証用として保持します。

GetLoadoutは常に独立した下書きを返し、Commitだけが確定状態を変更します。返した下書きやCommit後の入力オブジェクトを書き換えても保存状態は変わりません。同種AとBで形容詞・冠詞・技順・技ごとの副詞を別々に保持できます。

未変更の個体にはマスターの初期装備を使用し、セーブには変更した装備だけを保存します。最大4技、技の重複・不明なIDの拒否は従来どおりです。`GetDefaultLoadout` は種類マスターの初期値を見るための明示的なAPIで、`GetLoadout` に種類IDを渡して個体を暗黙に選ぶことはできません。

## 5. 選択・詳細・図鑑

GameManager.SelectedCharacterInstanceIdがプレイヤーの選択です。SelectedCharacterId／PlayerCharacterIdは種類IDの互換アクセサーで、同種の選択中個体があれば保持し、別種ならその種類の最初の所有個体を選びます。未所有種類のプレビューでは個体選択を空にします。装備の互換アクセサーも、選択中の個体へ接続します。

選択画面は所有する全個体を表示し、`Nacht · A1B2C3` のような短い識別表示と種類別所有数を付けます。選択中は青で示します。同じ短縮表示になる場合だけ識別部分を延長します。

詳細画面では名前の見出しを維持し、ステータス欄の先頭に `Instance: A1B2C3` を表示します。編集対象は画面を開いた個体IDに固定します。個体削除によりGameManagerが別個体へ選択を移しても、開いていた下書きを別個体へ保存することはありません。Save／Confirm／Discard／Cancelの既存動作も維持しています。

図鑑は種類ごとの表示です。`Undiscovered`、`Discovered • 0 owned`、`Owned: 2` を区別します。現在の個体を全て失っても発見記録は消えません。

図鑑から所有種類の詳細を開く場合、選択中の個体が同種ならその個体を維持し、それ以外ならその種類の最初の個体を使います。図鑑の選択欄にも詳細で使う個体を表示します。別の同種個体は選択画面から選べます。未所有の種類はマスターを閲覧するだけで、装備操作・確定・戦闘への不正な流用を防いでいます。

選択中IDは新たなセーブ項目には追加していません。ロード時に現在の選択が有効なら維持し、無効なら最初の所有個体を選択します。

## 6. 合成からの個体生成

キャラクター合成の「既に所有しているため生成できない」という制限を外し、毎回CharacterInstances.Createを呼びます。旧UnlockCharacterは互換性のため種類単位で一度だけ解放する動作を残し、新しい合成経路では使用しません。

レシピ登録・生成先の種類・必要素材を消費前に検証します。素材消費、個体生成、発見記録更新をPlayerStateChanges.BeginBatchでまとめ、1回の通知・自動保存にします。生成できなかった場合には消費素材を戻す経路も維持しています。セーブは全状態を1つのJSONへ原子的に保存します。

既存の `craft_wasserpistole`（word_wasser + word_pistole）は変更せず、繰り返して異なる個体IDを2つ生成することを確認しました。合成結果に種類別所有数も表示します。

## 7. セーブversion 2の構造

保存項目はversion、inventory、characterInstances、characterEquipment、characterCollection、stageProgressです。

```json
{
  "version": 2,
  "inventory": { "items": [] },
  "characterInstances": {
    "instances": [
      { "instanceId": "character-<永続ID>", "characterId": "nacht" }
    ]
  },
  "characterEquipment": { "characters": [] },
  "characterCollection": { "discoveredCharacterIds": ["nacht"] },
  "stageProgress": { "unlockedStageIds": [], "clearedStageIds": [] }
}
```

個体装備の各要素にはinstanceId、characterId、adjectiveId、articleId、skillSlotsを保存します。基本能力、画像、文章、戦闘中のHPや未来形予約・müssen使用状態は保存しません。

個体IDの重複、不明な種類、孤立・重複・種類不一致の装備、所有種類の発見記録欠落などを拒否します。version 2では所有0体も有効です。最後の個体を失って再起動しても、Nachtを勝手に再生成しません。

新規ゲームのみ、従来の初期種類Nachtを1体生成して発見済みにします。初期装備はマスター値、初期所持品とステージ進行は従来どおりです。

## 8. version 1からの移行

旧形式の実際のownedCharacters.characterIdsを読み、所有していた各種類に1体を割り当てます。種類別装備を対応する個体へ移し、所有していた種類を発見済みにします。所持品とステージ進行を維持します。

移行時のIDは、元JSONと種類IDに対するSHA-256の先頭128bitを使用します。同じ元データを再処理しても同じ個体IDとなるため、中断・再実行による増殖を防げます。通常入手時のIDとは生成方法を区別しています。

1. 旧データと移行結果を検証。
2. 元のバイト列を `save.json.v1.bak` へ退避。BOMも保持。退避は一時ファイルへの書き込み・Flush・移動で行う。
3. 同名の別内容バックアップがあれば上書きせず別名で保持。同じ元データの退避が既にあれば再利用。
4. version 2を一時ファイルへ書き、再検証してからFile.Replaceで本体を置き換える。通常の `.bak` も維持。
5. 次回以降はversion 2を読み、その個体IDをそのまま使用する。

退避・書き込み前の中断では旧本体が残り、置換後の中断ではversion 2が残ります。通常バックアップ・復旧ファイル・保護した旧形式バックアップからの回復も検証しました。移行失敗時はMigrationFailedとして元ファイルを残し、そのセッションの保存を禁止します。未対応バージョンも従来どおり保護します。

## 9. テストと結果

最終コードでEdit Mode 80件、Play Mode 27件、合計107件が成功、失敗0件です。従来の80件を含み、追加はEdit Mode 22件・Play Mode 5件です。既存のセーブ期待値・テスト準備を個体APIとversion 2へ更新しています。

個体の重複所有・ID一意性・取得コピー、個体削除、選択と装備の整理、図鑑の3状態と発見保持、独立した下書きと保存、繰り返し合成と失敗時の素材保持、version 2保存と0体復元、version 1装備引き継ぎ、BOMを含む元ファイル退避、再実行安全性、途中停止・バックアップ競合・破損・未対応版、戦闘中削除拒否を確認しました。

戦闘では別個体の装備で通常ダメージ14／15、未来形21／22、müssen28／30になることを確認しています。倍率の丸めも既存仕様のままです。副詞競合・予約勝利・敗北・音声などの既存テストも実行しました。

UI検証は3,391項目成功、失敗0件でした。個体識別子・ステータスの文字切れも検査しています。証跡は次のリンクを参照してください。4解像度（1920×1080、1280×720、1600×1200、2560×1080）で、選択・図鑑・詳細・合成・戦闘を含む21状態、84枚を確認しています。複数個体のクリック、2体目の装備保存、図鑑の3状態、同種2体の合成、既存の時制UIも検証します。

- [Edit Mode結果](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/edit-final-v2.xml)
- [Play Mode結果](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/play-final-v2.xml)
- [UI検証結果](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/ui-final-v2/validation.txt)
- [複数個体の選択](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/ui-final-v2/CharacterSelect-instanceB-1920x1080.png)
- [2体目の装備表示](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/ui-final-v2/CharacterDetail-instanceB-1600x1200.png)
- [繰り返し合成](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/ui-final-v2/Crafting-repeat-2560x1080.png)
- [保護対象照合](/Users/isao/Documents/ChatGPT/ゲーム/phase9-validation/integrity.txt)

Play Modeの新規テストにもTestSaveIsolationを指定し、Unity起動にも専用保存先を指定しました。実プレイヤー領域はテストの入力として使用していません。

初回テスト起動はライセンス接続待ちとなり、検証用プロセスだけを再起動しました。既存バックアップと内容が異なる場合の退避重複を追加テストで検出して修正し、最終結果は全件成功です。UI検証の保存ダイアログは表示更新後にクリックする手順へ直しています。

## 10. 未解決の問題・範囲

今回追加した機能の最終テストに失敗はありません。文字ドロップ、コイン、削除UI、分解報酬、強化、ステージ進行ルールは追加していません。

旧version 1に、未所有種類の装備だけが存在するなど対応先を一意に決められない状態がある場合、種類を勝手に付与したり装備を捨てたりせず、移行を停止して元ファイルを保護します。その場合はログに理由が出て、旧データの整合性修復が必要です。この安全側の停止もテスト済みです。

既存の文章・単語録音の不足による音声フォールバック警告は残ります。今回音声処理・素材は変更していません。

## 11. Unity Editorでの手動確認

元Editorでは再生を停止し、再コンパイル後にSceneを開き直してください。通常Playを始めると通常の保存先に対するversion 1→2移行が動きます。実データを消費せず確認する場合は、Play ModeのCharacterInstanceSceneTestsと保存移行のEdit Modeテストを使えます。これらは専用保存先を使います。

1. CharacterSelectで同種の2行を選び、短い識別子・青い選択表示・プレビューが切り替わることを確認。
2. 個体Aの詳細で装備を変更し、Back→Save。個体Bで元の装備が残り、Aへ戻ると変更が残ることを確認。Cancel／Discardも確認。
3. CharacterCollectionでOwned、Discovered・0 owned、Undiscoveredを区別できることを確認。未所有の詳細では装備を変更・戦闘開始できない。
4. 同じWasserpistoleレシピを2回合成し、所有数が2となり、選択画面で2体を区別できることを確認。実プレイヤーの素材を使いたくない場合は上記テストを利用する。
5. 個体を切り替えてBattleへ入り、それぞれの確定装備が反映され、通常攻撃・未来形・müssenが従来どおり動くことを確認。
6. 各画面を指定4解像度で確認。詳細の個体識別子はステータス欄の先頭に表示する。

## 12. 次の文字ドロップ・分解・強化に向けて

- 個体生成にはCharacterInstances.Create、対象指定にはinstanceIdを使用する。種類の既所有チェックや旧UnlockCharacterでは複数生成できない。
- 分解・強化はRemoveの成功を確認し、素材付与や強化状態更新とBeginBatchでまとめる。対象と消費個体を別IDで扱い、戦闘中の個体は消費しない。失敗時の取り消しも含めて設計する。
- 発見記録は所有個体の削除と独立して保持する。
- 強化値の仕様が決まった段階で個体固有状態と必要なセーブ移行を追加し、CharacterDataの基本能力は書き換えない。
- 現在のInventoryとセーブ検証は登録済みアイテムIDを検証する。任意の連続文字列素材を追加する際は、そのID登録・表記・保存検証を先に設計する。文字ドロップ処理だけを加えて未知IDを渡しても、そのまま保存可能にはならない。
