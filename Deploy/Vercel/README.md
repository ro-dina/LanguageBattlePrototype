# Unity Web版のVercel試験公開・更新手順

試験公開URL：<https://language-battle-prototype-trial.vercel.app/>

2026-11-06に個人Hobbyの`ro-dinas-projects`内へ`language-battle-prototype-trial`を新規作成し、初回公開と更新テスト1回を実施しました。GitHub連携、有料化、既存プロジェクトの上書きはしていません。

## 構成と公開範囲

```text
Deploy/Vercel/
├── .gitignore             # 生成物・アカウント固有情報をGitから除外
├── .vercelignore          # public/とvercel.jsonだけをアップロード
├── vercel.json            # 準備スクリプトが生成する配信設定
├── README.md              # この手順書、アップロード対象外
├── release-manifest.json  # 照合用の一覧・SHA-256、非公開・Git対象外
└── public/                # HTTPSで配信する12ファイル
    ├── index.html
    ├── Build/             # 内容のSHA-256をファイル名に使用
    └── TemplateData/      # HTML/CSSが参照する素材だけ
```

Unityプロジェクトのルートからデプロイしません。CLIが親のGit情報を取得することも防ぐため、検査済みパッケージを**どのGitリポジトリにも属さない別フォルダー**へコピーしてからデプロイします。`.vercel/`、トークン、実セーブ、設定実保存JSON、検証ログは公開しません。公開されたWebビルドにはゲーム実行用のコードと素材が含まれますが、Unityの編集用ソース・Scene・Prefab等は送りません。

## Webビルドの準備

Python 3.9以上、Node.js 20以上を使います。Brotli検査はNode標準ライブラリのみを使用します。Vercel CLIの検証版は65.12.1です。

UnityでWebビルドした後、Unityプロジェクトのフォルダーで実行します。`Builds/Web`は実際の出力先に読み替えます。

```sh
python3 -B Tools/prepare_vercel.py --source Builds/Web
python3 -B Tools/prepare_vercel.py --check
python3 -B Tools/test_vercel_packaging.py
python3 -B Tools/preview_vercel.py --port 8713
```

サーバーを動かしたまま、別のターミナルで検査します。

```sh
python3 -B Tools/verify_vercel_http.py http://127.0.0.1:8713/
```

準備スクリプトはアップロードしません。標準テンプレートのBrotliリリース出力4種類を検査し、ソース混入、symlink、対象パターンの秘密鍵・ローカルパス・実セーブ相当JSON等を検出すると停止します。Brotli内部も展開して検査します。すべての秘密情報を検出できる保証ではないため、入力は専用のWebビルドに限定します。

元ビルドを変更せず、配信用ファイル名とHTML参照を変更します。圧縮済みのゲーム本体は元のバイト列を維持します。別テンプレートやデバッグ出力を使う場合は、この検査に合わせて構成を見直します。

## 配信設定

- Other（frameworkはnull）、インストール・サーバービルドなし、Output Directoryは`public`。
- `.wasm.br`は`application/wasm`、`.framework.js.br`は`application/javascript`、`.data.br`は`application/octet-stream`。それぞれ`Content-Encoding: br`を指定。
- `.loader.js`は`application/javascript`。
- ハッシュ付きBuildは1年の`immutable`。HTMLとハッシュなしTemplateDataは`max-age=0, must-revalidate`。
- Unityのダウンロードキャッシュも、正確に一致するハッシュ付きdataファイルだけ`immutable`。Service Workerは追加しない。
- 起動前の`config.autoSyncPersistentDataPath = true`を維持。
- 起動時間・転送量・リリースIDをDOM属性へ記録。外部計測サービスへの送信なし。

根拠：[Unity配信](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-deploying.html)、[Unityキャッシュ](https://docs.unity3d.com/ja/6000.0/Manual/webgl-caching.html)、[Vercel設定](https://vercel.com/docs/project-configuration/vercel-json)、[HTTPキャッシュ](https://vercel.com/docs/caching/cache-control-headers)。

## 既存プロジェクトへの更新

次のアップロード操作は、更新についてユーザーから承認された範囲で実行します。今回承認された更新テスト1回は実施済みです。

1. 新しいWebビルドを準備・検査します。直前の成果物は必要に応じて別のローカルフォルダーへ保管します。
2. Git外へコピーします。以下は今回使用した一時フォルダーです。別の場所を使う場合もGit外にします。

```sh
python3 -B Tools/stage_vercel.py --destination /private/tmp/LanguageBattleVercelDeploy
```

3. Vercelへのログインが切れている場合だけ、ユーザー自身がブラウザで認証を承認します。パスワード・トークン・認証コードをチャットや文書へ保存しません。

```sh
npx --yes vercel@65.12.1 login
```

4. 送信対象を検査します。CLIのdry runにもログインと既存プロジェクトの指定が必要です。

```sh
npx --yes vercel@65.12.1 deploy --dry --json --cwd /private/tmp/LanguageBattleVercelDeploy --project language-battle-prototype-trial --scope ro-dinas-projects
```

今回の正常な一覧は`public/`12ファイルと`vercel.json`の計13ファイルです。README・manifest・.git・実セーブ・検証ログが含まれた場合は停止します。

5. 同じプロジェクトへProduction更新します。

```sh
npx --yes vercel@65.12.1 deploy --cwd /private/tmp/LanguageBattleVercelDeploy --project language-battle-prototype-trial --scope ro-dinas-projects --prod
```

新規作成用の`--create-if-missing`は更新には付けません。スコープやプロジェクトが見つからないときは設定を確認し、別プロジェクトを作らないでください。Git連携や有料プラン変更は不要です。

6. CLIのProduction URLとDashboardのDomainsを確認し、固定URLで検査します。

```sh
python3 -B Tools/verify_vercel_http.py https://language-battle-prototype-trial.vercel.app/
```

このHTTP検査は全12ファイルを取得し、MIME・Brotli・キャッシュと生の圧縮バイトのSHA-256を照合します。1回につき約58.2 MBの転送が発生します。ブラウザでリリース切替と既存の進行・設定保持も確認します。

今回の更新テストはリリース表示用メタデータだけを変更して実施し、固定URLのまま新版が読み込まれました。ゲームファイル自体を変更した配信の本番検証は、次回の実更新時にも行います。ローカルではLoader内容の変更でハッシュURLが変わることを検証済みです。

## 別の公開先を新規作成する場合

現在の試験プロジェクトは作成済みです。別プロジェクトを作る必要はありません。新規作成が必要な場合は、個人Hobbyのスコープと同名プロジェクトの不存在を確認し、ログイン・新規作成・アップロード・Production公開・URL変更について事前承認を得ます。承認後に新しい名前と`framework: null`、`outputDirectory: public`、ビルド・インストールなしで作成します。既存プロジェクトを無断で上書きしません。

継続プレイ用に案内するのはProductionの固定URLです。デプロイ固有URLやPreview URLは案内しません。ホスト名・ポート・配信パスを変えると保存が自動で移行するとは限りません。localhost・Preview・試験公開・将来の本番公開の保存は区別します。

## ブラウザ確認と保存の限界

初回起動、言語・音量変更、Stage 1受講・戦闘・勝利・報酬、再読み込み、通常リセット後の再読み込み、2回目のリセットを試験データで確認します。Chrome系で実施済みです。Safari・スマートフォンは未確認です。

保存成功表示は従来のファイル処理成功を表し、非同期のIndexedDB同期完了を厳密には待ちません。強制終了・容量不足・保存拒否は未検証です。将来強化するなら、同期完了・失敗をWebから受け取って表示する小さな連携を検討します。v6形式や保存機構は今回変更していません。

## ロールバック

Dashboardで直前のProductionデプロイからInstant Rollbackを選びます。Hobbyで戻せるのは直前のProductionです。今回の直前リリースは`38e201ad546f3ccee16309ed`、現在は`b4f8102a7fb6da0e52ae067c`です。ロールバック後は自動Production割り当てが停止するため、修正版を確認して対象デプロイをPromoteし通常運用に戻します。実施時に対象を確認します。今回は手順の文書化のみで、ロールバックはしていません。

配信のロールバックは利用者のブラウザ保存を巻き戻しません。[公式手順](https://vercel.com/docs/instant-rollback)

## 公開停止・再開・削除

- 停止：対象プロジェクト → Settings → General → Pause Project。Productionは503になり、停止後はデプロイ固有URLを含む公開状態も確認します。
- 再開：SettingsのResume Service。通常は再デプロイ不要です。
- 削除：Delete Projectはデプロイ・ドメイン・環境変数・設定も削除します。削除時は別途承認を得ます。期間限定展示の終了にはPauseを使えます。削除後の固定URLの再取得は保証されません。
- 停止・削除しても、開いているゲームの実行やブラウザ内セーブを遠隔で消せるとは限りません。サイトデータ削除、端末変更、ブラウザの領域整理で保存が失われることがあります。

今回は公開停止・削除はしていません。[公式手順](https://vercel.com/docs/projects/managing-projects)

## Hobbyの条件と容量

2026-11-06確認。Hobbyは個人・非商用向けです。今回は無料で広告・販売のない個人作品展示として承認されています。Fast Data Transfer 100 GB、Fast Origin Transfer 10 GB、CDNリクエスト100万回が含まれます。CLIアップロード元合計100 MB、デプロイ100回/日です。今回の送信パッケージ約58.2 MBは上限内です。

全員がキャッシュなしで1回読み込む概算は100人で5.82 GB、500人で29.1 GB、1,000人で58.2 GB、1,500人で87.3 GB、2,000人で116.4 GBです。ヘッダー、再訪問、他プロジェクト等は別途です。Usage表示には反映の遅れがあるため、開催前と開催中に確認します。Hobbyは枠超過で配信や機能が停止し、回復を待つ場合があります。条件内の想定費用は0、有料プラン変更・課金設定はしていません。

[Hobby](https://vercel.com/docs/plans/hobby)、[Limits](https://vercel.com/docs/limits)、[商用利用条件](https://vercel.com/docs/limits/fair-use-guidelines)、[超過時](https://vercel.com/docs/plans)
