# Phase 11.1 — Vercel試験公開・検証レポート

実施日：2026-11-06（日本時間）

状態：**試験公開と更新テスト1回を完了**。固定URLは <https://language-battle-prototype-trial.vercel.app/> です。ログイン不要のHTTPSアクセスで、起動・Stage 1受講・戦闘・勝利報酬・保存・再読み込み・通常リセット2回を確認しました。Chrome系で検証済み、Safari・実機スマートフォンは未確認です。

## 採用方式と環境

Unity 6000.4.8f1のPhase 11.0 Web成果物を静的パッケージにし、Vercel CLIで手動公開しました。ユーザーからログイン・個人Hobbyの新規プロジェクト作成・約58.2 MBのWeb成果物のみのアップロード・Production公開・更新テスト1回の承認を得て実施しました。認証はユーザー自身がブラウザで完了しています。

元プロジェクトは`/Users/isao/Documents/Unity_projecy/LanguageBattlePrototype`。開始時はmain、`972d2c8 web`、未コミット変更なしでした。既存GitHubリポジトリ`ro-dina/LanguageBattlePrototype`は公開APIでpublicと確認しました。公開範囲の変更、commit、push、reset、GitHub連携は行っていません。

Vercel CLIは当初PATH上に存在せず、検証フォルダー内だけへ65.12.1を導入しました。Node.jsはv22.19.0。個人スコープ`ro-dinas-projects`のHobbyで、同名の既存プロジェクトがないことを確認してから新規作成しました。Git外のステージングフォルダーから送り、初回デプロイのAPI確認でもgitSourceはnull、Git由来メタデータのキーは空でした。認証情報はレポートへ記録していません。

| 方式 | 更新・公開範囲 | 判断 |
|---|---|---|
| Unityと成果物を同じ非公開Gitで管理 | 自動更新しやすいがVercelへソース取得権限を渡す必要がある。現在のリポジトリはpublic | 今回不採用 |
| Web成果物だけの別Git | 外部へ渡す範囲を分けやすいが、新規リポジトリと連携の管理が増える | 将来の候補 |
| CLIの手動公開 | 実行成果物と配信設定だけを送れる | 今回採用 |

## 構成と追加ファイル

`Deploy/Vercel/public/`が配信ルートです。`.vercelignore`はpublicとvercel.jsonだけを許可し、さらに`Tools/stage_vercel.py`でGit外の`/private/tmp/LanguageBattleVercelDeploy`へコピーします。認証済みCLIのdry runでは、実際の送信一覧がpublic内12ファイルとvercel.jsonの**計13ファイルだけ**でした。文書、照合manifest、Unityソース、実セーブ、検証ログ、.git、アカウント固有情報は送りません。

```text
Deploy/Vercel/
├── .gitignore / .vercelignore
├── vercel.json
├── README.md
├── release-manifest.json  # 非公開・Git対象外
└── public/               # Git対象外
    ├── index.html
    ├── Build/            # Brotli3ファイルとLoader、計4
    └── TemplateData/     # 参照する素材7ファイル
```

追加した管理用ファイルは10個です。

- `Tools/prepare_vercel.py`：生成・内容検査・整合性検査。
- `Tools/stage_vercel.py`：Git外の送信専用フォルダーへ検証済み成果物をコピー。
- `Tools/preview_vercel.py`：ローカル配信検査。
- `Tools/verify_vercel_http.py`：HTTPヘッダー・受信バイト・非公開パス404検査。
- `Tools/test_vercel_packaging.py`：公開境界のガードテスト8件。
- `Deploy/Vercel/.gitignore`、`.vercelignore`、`vercel.json`、`README.md`。
- このレポート。

public内12ファイルと非公開manifestの計13生成ファイルはGit対象外です。Phase 11.0の元ビルド`/Users/isao/Documents/ChatGPT/ゲーム/phase110-validation/web`は存在し、再ビルドせず再利用しました。元ビルドを変更していません。圧縮済みゲームファイルの生バイトを維持し、配信用ファイル名とHTML参照を変更しています。

## 配信・キャッシュ・保存の設定

Other（frameworkはnull）、インストール・ビルドコマンドなし、Output Directoryはpublicです。実在する各BuildパスへMIMEと圧縮ヘッダーを指定しました。

| ファイル | Content-Type | Content-Encoding |
|---|---|---|
| wasm.br | application/wasm | br |
| data.br | application/octet-stream | br |
| framework.js.br | application/javascript | br |
| loader.js | application/javascript | なし |

Build4ファイルの名前に各内容のSHA-256を使用し、1年のimmutableキャッシュを設定しました。HTMLとハッシュなしTemplateDataは毎回再検証します。Unityのダウンロードキャッシュも、正確に一致するハッシュ付きdata URLだけimmutableにします。内容が変わればURLが変わります。Service Workerやサーバー機能は追加していません。

`config.autoSyncPersistentDataPath = true`は起動前に維持しています。起動時間・転送量・リリースIDはDOM属性への記録のみで、外部計測サービスへの送信はありません。SaveFileStore、SaveGenerationStore、PlayerSaveService、NewGameDefaults、進行v6、UserSettings version1は変更していません。

根拠：[Unity圧縮配信](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-deploying.html)、[Unityキャッシュ](https://docs.unity3d.com/ja/6000.0/Manual/webgl-caching.html)、[Vercel設定](https://vercel.com/docs/project-configuration/vercel-json)、[Cache-Control](https://vercel.com/docs/caching/cache-control-headers)。使用項目は公式スキーマの未改変の各定義でも検査しました。スキーマ全体にはdraft-04指定と新しい実験用キーワードの混在があり、全体コンパイル成功とは扱っていません。CLI受付と実HTTPS検査は別途成功しています。

## デプロイ結果

個人Hobbyの`language-battle-prototype-trial`へProductionを2回作成しました。1回目が初回公開、2回目が承認された更新テストです。固定URLは同一です。

| 内容 | リリースID | Vercel結果 |
|---|---|---|
| 初回 | 38e201ad546f3ccee16309ed | READY / Production |
| 更新後・現在 | b4f8102a7fb6da0e52ae067c | READY / Production |

初回デプロイID：`dpl_FSiXfra7S1dTs5SeE9r9ncAW6bcE`。現在：`dpl_9Ezz3wMnETZVvqFJLzpX6FPpVd1F`。

更新テストではHTMLのリリースメタデータと対応するレスポンスヘッダーだけを変更しました。固定URLで新しいIDのHTMLが読み込まれ、同じゲームファイルが再利用され、進行と設定が残ることを確認しました。ゲーム本体を変更した実公開の混在検査までは主張しません。ローカルではLoaderの内容変更によるハッシュURL更新を別途検査しています。

## HTTP・ブラウザの検証

公開URLを認証情報なしで取得し、初回・更新後とも全12ファイルが200、MIME・Brotli・Cache-Controlが正常、受信した生バイトのSHA-256がローカル成果物と一致しました。配信側の再圧縮による不一致はありません。README、vercel.json、manifest、.vercel、.git、Unityソース、進行・設定JSONに相当する8つの非公開パスは404でした。

ブラウザはChatGPT内蔵のChromium系で検証しました。

| 操作 | 実際に観測した結果 |
|---|---|
| 初回起動 | HTTPSのHomeが表示され、3,000 Coins |
| 設定 | 英語へ変更、マスター50%、ボイス60%。音声プレビューボタン操作で例外なし。実際の聴感は未確認 |
| Stage 1受講 | キャラクターを選択し受講開始。未紹介の副詞・müssen・Futur Iは無効のまま |
| 戦闘・勝利 | seinで4回攻撃し勝利。通常100＋初回300＝400 Coins、素材報酬、受講完了を表示 |
| 再読み込み | 3,400 Coins、Stage 1 Cleared 1、Stage 2 Ready。再受講表示と設定も保持 |
| Production更新後 | 新リリースID、3,400 Coins、クリア・解放状態、英語と音量50/60%を保持。報酬の重複なし |
| 通常リセット1回目 | 成功表示後に再読み込み。3,000 Coins、Stage 1 Ready、Stage 2 Locked。受講は未受講の表示へ戻る |
| 2回目の前準備 | Skip allを有効にし、再読み込み後も「Tutorial prompts: skipped」が残ることを確認 |
| 通常リセット2回目 | 成功後に再読み込み。3,000 Coins、Stage 1 Ready、Stage 2 Locked、Tutorial prompts: enabled。古いクリア・スキップ状態が復活しない |
| 設定の保護 | 2回のリセット後も英語、マスター50%、ボイス60%が残る |
| Console | 最終確認時のerrorレベルは0件 |
| Safari・実機スマートフォン | 未検証。Safariはユーザー操作との競合があったため操作を中止 |

公開URLの保存判定はゲーム画面と再読み込み後の観測に基づきます。公開ブラウザの世代ポインターJSONを直接読み取った検査ではありません。ブラウザ内の試験データは最後にリセット済みです。localhostやデスクトップの実セーブは使用していません。

## 自動テストと既存データの保護

| 検査 | 結果 |
|---|---:|
| EditMode | 206 / 206成功 |
| PlayMode | 86 / 86成功 |
| 公開境界のテスト | 8 / 8成功 |
| Git内へのステージング拒否 | 拒否を確認 |
| 既存ファイル | 開始時の802ファイルのSHA-256一致 |
| 実進行 | 132ファイルのSHA-256・更新時刻一致 |
| 実設定 | 2ファイルのSHA-256・更新時刻一致 |

Unityテストは検証コピーと専用保存先で実施しました。ゲーム本体・Scene・Prefab・meta・GUID・Inspectorイベントは変更していません。Gitの変更は今回の新規管理ファイルだけで、既存差分を整理・破棄していません。検証記録は`/Users/isao/Documents/ChatGPT/ゲーム/phase111-validation`です。

## 容量・ロード実測・費用

public合計は58,197,065 bytes（約58.2 MB）。data.brは49,204,185 bytes、wasm.brは8,872,121 bytes。配信設定を含むCLI送信対象は58,199,661 bytesで、HobbyのCLIアップロード元100 MB以内です。[Limits](https://vercel.com/docs/limits)

| 固定HTTPS URLでの計測 | Unity起動完了 | その時点の計測転送量 |
|---|---:|---:|
| ブラウザ初回 | 5,406 ms | 58,178,605 bytes |
| キャッシュあり再読み込み | 398 ms | 2,400 bytes |
| 更新後の再読み込み | 855 ms | 5,177 bytes |

単一環境でのサンプルです。HTMLがUnity起動完了時に取得したNavigation/Resource Timingの合計で、全通信の厳密な課金値ではありません。初回はHTTP検査によるCDNキャッシュ温存の影響があり得ます。独立した全12ファイルの逐次HTTP検査は初回12.017秒、更新後12.131秒でしたが、並列読込を行う実ゲームのロード時間とは別です。

以下は全員が初回に約58.2 MBを受け取る仮定の概算で、実測と区別します。

| 初回アクセス | 転送概算 |
|---|---:|
| 100人 | 5.82 GB |
| 500人 | 29.1 GB |
| 1,000人 | 58.2 GB |
| 1,500人 | 87.3 GB |
| 2,000人 | 116.4 GB |

ヘッダー、再訪問、途中中断、キャッシュ、他プロジェクト等で変動します。ユーザー確認済みの無料・広告販売なしの個人作品展示としてHobbyを使用します。個人・非商用という条件は用途が変われば再確認が必要です。[商用利用条件](https://vercel.com/docs/limits/fair-use-guidelines)

HobbyにはFast Data Transfer 100 GB、Fast Origin Transfer 10 GB、CDNリクエスト100万回、デプロイ100回/日が含まれます。枠超過では配信停止や枠回復待ちが起こり得ます。条件内の想定費用は0で、有料化・課金設定は実施していません。[Hobby](https://vercel.com/docs/plans/hobby)、[超過時](https://vercel.com/docs/plans)

DashboardのLast 30 daysはFast Data Transfer 1.53 MB / 100 GB、Fast Origin Transfer 737.7 kB / 10 GBと表示されました。カウンターの反映遅延があり、今回の検査転送をすべて反映した残量とは扱いません。文化祭前と開催中に、他プロジェクトの消費も含めUsageを確認します。

## 更新・停止・ロールバック

詳細は`Deploy/Vercel/README.md`に記載しました。更新はWebビルド→prepare/check→Git外stage→CLI dry run→同じプロジェクトへProduction deploy→HTTPSとブラウザ保存を検査、の順です。固定URLのホスト名と配信ルートを維持します。Preview・localhost・将来の別オリジンの本番サイトへ保存が自動移行するとは案内しません。

HobbyのInstant Rollbackは直前のProductionへ戻します。ロールバック後は自動Production割り当てが停止するため、修正版をPromoteして戻します。ブラウザ保存は巻き戻しません。今回ロールバックは実施していません。[公式手順](https://vercel.com/docs/instant-rollback)

公開停止はProject → Settings → General → Pause Project、再開はResume Serviceです。削除はデプロイ・ドメイン・設定も消すため、期間限定展示の終了にはPauseを使えます。削除してもブラウザ保存を遠隔削除できるとは限らず、固定URLの再取得も保証されません。今回停止・削除は実施していません。[停止・削除](https://vercel.com/docs/projects/managing-projects)

## 残る確認と次のPhase

保存成功表示は既存のファイル処理成功を表し、非同期のIndexedDB同期完了を厳密に保証しません。強制終了・容量不足・保存拒否は未検証です。次の段階では同期完了・失敗を受け取る小さなWeb連携を検討できます。今回は保存APIや世代切り替えを作り直していません。

Safari、文化祭で使用する実端末・回線、実際の音声の聴感は追加確認の対象です。既存のTechnik画像・一部音声素材不足、FSR関連警告、狭い画面でのUIは今回変更していません。次の実ゲーム更新では新しいハッシュ付きゲームファイルの本番切替も確認します。
