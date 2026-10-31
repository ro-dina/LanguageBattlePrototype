# Phase 9.1 実装・検証レポート

実施日: 2026-10-31 / Unity 6000.4.8f1

種類別の一覧から所有個体を切り替える操作、ドイツ語30文字のガチャ排出、Futur I・müssenのトグル操作を実装しました。セーブ形式はversion 2を維持しています。

## 1. 修正・追加ファイル

すべてプロジェクトルートからの相対パスです。

| ファイル | 変更内容 |
|---|---|
| Assets/Scripts/CharacterSelectManager.cs | 種類別一覧、所有数、個体の左右切替、装備と補正後能力値の表示 |
| Assets/Scripts/CharacterInstances.cs | 個体の表示識別子をUUID由来の文字から「1 / 3」形式の番号に変更 |
| Assets/Scripts/CharacterDetailManager.cs | 編集対象の個体番号を表示 |
| Assets/Scenes/CharacterSelect.unity | 左右ボタン、個体番号表示、Inspector参照追加、プレビュー文字サイズ調整 |
| Assets/Resources/Data/gacha_items.json | 未登録だった21文字を追加。既存16項目は変更なし |
| Assets/Scripts/GachaTable.cs（新規） | 従来のレアリティ抽選を固定条件で検証できる抽選表に抽出 |
| Assets/Scripts/ItemDisplayNames.cs（新規） | 内部IDとは別の共通表示名参照。既存のdisplayNameを利用 |
| Assets/Scripts/GachaManager.cs | 従来の2段階抽選をGachaTable経由で実行、共通表示名を利用 |
| Assets/Scripts/InventoryUIManager.cs | 内部IDの代わりに表示名を表示 |
| Assets/Scripts/CraftingUIManager.cs | 重複していた表示名読込を共通参照に統一 |
| Assets/Scripts/BattleSelection.cs | 他方の修飾選択を解除してから選択する排他処理、履歴の記録 |
| Assets/Scripts/BattleManager.cs | 未来形とmüssenを直接切替できる選択処理 |
| Assets/Scripts/BattleModifierUI.cs | 再クリックで解除、ON/OFF表示、他方のトグルを切替用に有効化 |
| Assets/Scripts/BattleActionResolver.cs | 有効なmüssenの技コマンド確定時に使用済み化。ダメージ適用は従来のタイミング |
| Assets/Scenes/Battle.unity | Futur I・müssenのボタン幅調整、Default tense・No modalを非表示 |
| Assets/Tests/EditMode/GachaContentTests.cs（新規） | 30文字、確率、全抽選経路、ラベル、version 2保存の検証 |
| Assets/Tests/EditMode/BattleModifierTests.cs | 確定時の消費と切替履歴の検証 |
| Assets/Tests/PlayMode/CharacterInstanceSceneTests.cs | 種類別一覧、3個体切替、装備表示・戦闘、削除、保存、ガチャ表示の検証 |
| Assets/Tests/PlayMode/BattleModifierSceneTests.cs | トグル、切替、消費、確定した予約の解除防止、既存仕様の検証 |
| Tools/BuildPhase91UI.cs（新規） | 作業用コピーでScene UIを生成する補助ツール |
| Tools/UIPlayValidation.cs | 個体切替と新トグルの実クリック、特殊文字、4解像度検証を更新 |
| Documentation/Phase9.1_個体選択・ガチャ・戦闘UI改善レポート.md（本書） | 変更・検証結果 |

新規C#アセット3件に対応するmetaを追加しています。既存metaは変更していません。合計25ファイル（meta含む）。アプリケーション側の新規クラスはGachaTableとItemDisplayNamesです。

## 2. 重複キャラクターを選択できなかった原因の調査

変更前のCharacterSelectManagerは、CharacterInstances.GetAll()から所有個体ごとにボタンを生成し、個体IDをコールバックへ渡していました。「種類IDで重複排除してしまう」処理はありませんでした。

実セーブの所有数を読み取り専用で確認した時点では、version 2にNachtの個体は1体だけ記録されていました。したがって、この環境では「複数個体が保存されているのに1体しか表示されない」という原因は確認できませんでした。実セーブをテスト入力へ流用していません。

一方、旧UIは同名の個体を別行に並べ、UUID由来の短い識別文字を付ける方式で、種類の選択と個体の選択を明確に分けていませんでした。今回、希望された種類別一覧と明示的な個体切替へ変更しています。未所有個体を追加したり、実セーブに個体を補充したりはしていません。

## 3. 新しい個体切替UIの操作

- 左の一覧は「Nacht ×3」「Feuer ×2」のように種類ごとに1行表示します。
- 種類を選び、能力値欄の下にある「<」「>」で所有個体を切り替えます。
- 「Individual 2 / 3」が現在の表示番号です。末尾から先頭、先頭から末尾へ循環します。
- 1体のみの場合は「Individual 1 / 1」と表示し、左右ボタンを無効にします。0体では選択を解除します。
- 装備と能力値を確認し、Detailsでその個体の装備編集、Start Battleでその個体を戦闘へ渡します。

個体順は所有リストの順です。表示番号は保存キーではありません。並べ替え・削除・復元で番号が変わっても、選択中の実instanceIdを使用します。詳細画面と図鑑からの個体表示も番号へ統一し、UUIDを表示しません。

## 4. 個体別装備と戦闘の検証

3体のNachtを作成し、schwarz、schnell、初期装備を別々に設定して、左右切替・プレビュー・戦闘開始まで検証しました。各戦闘のPlayerInstanceIdと確定装備が選んだ個体に一致しました。

既存の詳細画面テストでは、個体Bの保存がAへ漏れないこと、Aへ戻った際に保存内容が残ること、下書き、Confirm、Save、Discard、Cancelの操作を引き続き検証しています。削除した個体を選択できないこと、選択中の個体を削除すると有効な個体へ切り替わることも確認しました。

装備、所有個体、発見記録、ステージ進行のセーブ構造・個体IDは変更していません。CharacterLoadoutStoreと個体別の戦闘取得経路も維持しています。

## 5. ガチャの文字が限られていた原因

GachaManagerはgacha_items.jsonから全項目を読み、まずレアリティを抽選し、そのレアリティ内で均等に抽選していました。文字を限定する別のリストはありませんでした。

gacha_items.jsonの文字項目がa・w・p・i・s・t・o・l・eの9種類しかなかったため、ほかの文字は抽選対象になっていませんでした。インベントリでletter_aなどと見えていたのは、InventoryUIManagerがdisplayNameではなくitemIdを表示していたためです。

## 6. 30文字の登録と排出確率

26文字とä・ö・ü・ß、計30種類を通常文字（N）へ登録しました。既存9文字と非文字報酬7項目のID・定義はそのままで、21文字を追加しています。

| 抽選枠 | 合計確率 | 枠内項目 | 1項目の確率 |
|---|---:|---|---:|
| N | 60% | ドイツ語30文字 | 2% |
| R | 25% | sch、ss、er、Wasser、Pistole | 5% |
| SR | 12% | -ung | 12% |
| SSR | 3% | Nacht（word_nacht素材） | 3% |

全37項目で100%です。既存のレアリティ配分と枠内均等抽選を維持したため、以前の9文字が占めていたN枠を30文字で分ける形になります。非文字報酬の確率は変わりません。

特殊文字の内部IDはletter_ae_umlaut、letter_oe_umlaut、letter_ue_umlaut、letter_eszettです。表示はä・ö・ü・ßのままで、ae・oe・ue・ssへの変換や大文字の別素材は追加していません。

GameContentIdsは既存どおりマスターの全IDを保存検証へ取り込みます。version 2のまま新文字を保存・復元でき、旧文字の数量、個体別装備、個体IDが残ることを確認しました。単発・10連の回数、報酬追加・通知のまとめ方、既存表示の流れは維持しています。

## 7. インベントリ表示

共通のItemDisplayNames.Get(itemId)で、既存マスターのdisplayNameを取得します。インベントリは「a ×54」「ä ×3」「ß ×2」のように表示します。ガチャ結果と合成素材の一覧・選択中素材も同じ参照を使います。

合成のä + ö + ü + ßが表示上でäöüßとなることも検証しています。今回この並びに新レシピは追加していません。既存のWasserpistoleレシピは変更していません。

既存のschなどの表示メタデータも共通参照を使えます。将来の任意文字列素材はdisplayNameを登録する構造を利用できますが、動的生成・ドロップ・分解は追加していません。

## 8. Futur I・müssenのトグル

Futur Iまたはmüssenを押すとON、同じボタンをもう一度押すとOFFです。ONは青い背景とONの文字で表示し、OFFは白い背景とOFFの文字で表示します。他方を押したときは選択を直接切り替えます。

Default tenseとNo modalはGameObjectを非表示にして、既存fileIDと参照を残しました。空いた領域へ2つのトグルを広げ、下部パネルの範囲と背景・キャラクター画像を維持しています。

müssenは選択・解除だけでは消費しません。有効な技が確定してBattleContext.Resolveに受理された時点でMuessenUsedを立てます。ダメージの適用は従来どおり音声・演出後のApplyです。無効な組み合わせは受理されず消費しません。使用済みの表示・次の戦闘でのリセット・セーブに保存しない扱いは維持しています。

未来形は予約時0ダメージ、次の自分のターンの通常入力前に1.5倍で発動、発動後の通常行動、1件の予約制限、勝敗・離脱時の破棄を維持しています。確定済み予約をUIから取り消す処理はありません。

## 9. 排他制御と選択履歴

BattleSelection.Selectでfutur1を選んだ際は助動詞を、muessenを選んだ際は時制を解除してから新しい選択を記録します。UI以外からこのAPIを呼んでも両方ONにはなりません。

解除イベントには元のID、種類、連番を記録し、その後の選択イベントとともにCommit時の不変スナップショットへ残します。選択解除と変更の履歴も保持します。選択順による戦闘効果は追加していません。

BattleManagerは予約済み・使用済み・処理中・言語の条件を先に検査し、選べない操作で現在の有効な選択が消えないようにします。BattleActionResolverによる不正な未来形＋müssenの拒否も維持しています。

Clear modifiersは未確定の副詞・時制・助動詞のみを解除します。処理中は受け付けず、確定した予約やMuessenUsedには触れません。技のみで通常攻撃でき、副詞競合、DamageCalculator、SentenceGenerator、AttackAudioPlaybackの従来動作も維持しています。

## 10. Edit Mode / Play Mode

最終の全件実行結果:

| 対象 | 成功 | 失敗 |
|---|---:|---:|
| Edit Mode | 86 | 0 |
| Play Mode | 31 | 0 |
| 合計 | 117 | 0 |

主な追加確認は、3個体の循環・実IDの区別・表示順変更・個体別装備のプレビューと戦闘・削除後の再選択・保存復元、30文字の全抽選経路と正の確率・合計100%・旧数量保持・特殊文字の表示と保存、修飾トグルのON/OFFと相互切替・履歴・使用回数・確定予約の保護です。

抽選は少数のランダム試行で全文字が出ることを期待せず、明示したレアリティロールと候補番号で全経路を検証しました。Sceneの報酬確認は固定シードで行い、元の乱数状態を復元しています。

既存の装備、合成、発見記録、version 1移行・version 2復元・破損復旧・未対応バージョン保護、未来形、müssen、副詞競合、ダメージ計算、三人称・分離動詞の文章生成、勝敗のテストをすべて実行しています。

実行証跡: [Edit結果](/Users/isao/Documents/ChatGPT/ゲーム/phase91-validation/edit-final.xml)、[Play結果](/Users/isao/Documents/ChatGPT/ゲーム/phase91-validation/play-final.xml)。

## 11. UI検証

1920×1080、1280×720、1600×1200、2560×1080で計92枚を実描画しました。指定5画面に加え、図鑑、合成、装備一覧、保存確認、未来形選択中・予約中、müssen使用済みの状態も確認しています。

4,259項目成功、失敗0。ボタンの画面内配置、領域の大きさ、一覧行の重なり、参照、個体・装備テキストの収まりを検査し、個体左右ボタンと修飾トグルは各解像度でポインターが届くことを確認しました。実クリックで3個体の切替、詳細での個体Bの保存、トグルの再クリックと相互切替、未来形の勝利、müssenの使用済み表示も確認しました。

15 Scene/Prefabの参照検査が成功しています。既存Scene/PrefabのfileIDとGUID、既存Inspectorイベントを保持し、CharacterSelectに新規UI要素のfileIDを追加しただけです。既存meta・Prefab・背景・キャラクター画像は変更していません。

画像・検証記録: [UI結果](/Users/isao/Documents/ChatGPT/ゲーム/phase91-validation/ui/validation.txt)、[個体切替の画面](/Users/isao/Documents/ChatGPT/ゲーム/phase91-validation/ui/CharacterSelect-instanceC-1280x720.png)、[戦闘トグルの画面](/Users/isao/Documents/ChatGPT/ゲーム/phase91-validation/ui/Battle-future-selected-1920x1080.png)。

## 12. 残っている問題と手動確認

今回追加した機能で未解決の失敗はありません。既存の音声ファイル不足による警告は8種類残ります。文章音声から単語音声へのフォールバックは動作しており、音声素材の追加はしていません。今回のUI実行ではUnity内部SearchDatabaseの診断は0件でした。

元プロジェクトのEditorが起動中だったため、実装・テストは作業用コピーで行いました。実プレイヤーのセーブは内容ハッシュ・作成日時・更新日時を作業前後で照合し、変更がないことを確認しました。元のGit状態は変更なしでした。適用対象の旧ファイルは別途退避し、既存変更を上書きしない照合を行いました。

Editorでは次を確認してください。

1. 編集中のPlay Modeを終了して再インポートを待ち、CharacterSelectを開く。現在の実データはNacht 1体なので、左右ボタンが無効な「Individual 1 / 1」が正常です。
2. 通常のゲーム操作で同じ種類を複数入手した状態では、種類の1行から左右で切り替え、Detailsで装備変更・Save後、別個体へ影響していないことを見る。既存のWasserpistole合成も繰り返し個体を生成できます。
3. ガチャを引き、Inventoryで内部IDの代わりに文字が表示されることを見る。各文字2%なので少ない試行で全30文字は揃いません。
4. 戦闘でFutur I・müssenの再クリック、相互切替、Clear modifiers、技のみの攻撃、予約攻撃、müssen使用後の表示を見る。

ドロップ、コイン、分解、強化、追加助動詞効果は未実装のままです。これらを追加するときも、表示番号を保存キーにせずinstanceIdを使い、素材の表示名を保存IDから分離してください。
