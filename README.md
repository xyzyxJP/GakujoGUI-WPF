<br />
<div align="center">
	<img src="https://github.com/xyzyxJP/GakujoGUI-WPF/blob/main/GakujoGUI/Resources/GakujoGUI.png?raw=true" width="128">
	<h2 style="font-weight: 600">GakujoGUI</h2>
	<p align="center">
	WPF Version<br>
	静岡大学 学務情報システム
	</p>
  	<img src="https://github.com/xyzyxJP/GakujoGUI-WPF/blob/main/GakujoGUI/Resources/MainForm.png?raw=true" width="640">
</div>

## ✨ Feature

- ✅ 学務情報システムのアプリケーション
- 💾 オフライン起動可能なローカル保存
- 🚧 メンテナンス / サーバーダウン時に閲覧可能 (事前に更新したデータに限る)
- 🕰️ 読み込み時間を大幅短縮
- 📅 時間割に未提出の課題を強調表示
- 💬 授業連絡などの内容を検索可能
- 🔄 バックグラウンドで自動更新
- 🛎️ 新着通知をDiscordでお知らせ
- 📝 未提出の課題をTodoistで管理
- 🌙 ライト / ダークテーマに対応

## 📦️ Installation

[Release](https://github.com/xyzyxJP/GakujoGUI-WPF/releases)

![image](https://user-images.githubusercontent.com/8305330/155837127-97902d39-1612-4132-8d77-db1747f46b68.png)

Zipファイルをダウンロードし，すべて展開を行ったあとに，`GakujoGUI.exe`を実行してください．

## ⚙️ Usage

### ログイン

- 静大ID，パスワードをもとに学務情報システムへログインする
- CSS，画像ファイルなどを取得しないため，ブラウザ上のログインより高速である

### 保存データ

- 全てのデータはローカル内に保存され，外部にアップロードされることはない

#### アカウント

- 静大ID
- パスワード
- 学生氏名
- 学籍番号
- トークン
- 更新日時

#### 授業連絡

- 授業科目 学期/曜日時限
- 担当教員氏名
- 連絡種別
- タイトル
- 内容
- ファイル
- ファイルリンク公開
- 参考URL
- 重要度
- 連絡日時
- WEB返信要求

#### レポート

- 授業科目 学期/曜日時限
- タイトル
- 状態
- 提出期間
- 最終提出日時
- 実施形式
- 操作
- `ReportId`
- `SchoolYear`
- `SubjectCode`
- `ClassCode`

#### 小テスト

- 授業科目 学期/曜日時限
- タイトル
- 状態
- 提出期間
- 提出状況
- 実施形式
- 操作
- `QuizId`
- `SchoolYear`
- `SubjectCode`
- `ClassCode`

#### 授業共有ファイル

- 授業科目 学期/曜日時限
- タイトル
- サイズ
- ファイル
- ファイル説明
- 公開期間
- 更新日時

#### 成績情報

- 科目名
- 担当教員名
- 科目区分
- 必修選択区分
- 単位
- 評価
- 得点
- 科目GP
- 取得年度
- 報告日
- 試験種別

<br />

- 評価別単位
- 学部内GPA
- 学科等内GPA
- 年別単位


## ⚠️ Troubleshooting

![image](https://user-images.githubusercontent.com/8305330/155836834-c9401ec2-e2c2-4d54-991a-30edfe8c314a.png)

上記画像のエラーが表示された場合は，実行に必要なランタイムが不足しています．

[https://dotnet.microsoft.com/ja-jp/download/dotnet/6.0/runtime](https://dotnet.microsoft.com/ja-jp/download/dotnet/6.0/runtime)

上記リンクよりランタイムをインストールしてください．

![image](https://user-images.githubusercontent.com/8305330/155836978-a9be93bb-2636-47bc-9ac6-b73293c4bb22.png)

ページ中央にある`デスクトップアプリを実行する`の`X64のダウンロード`をクリックしてください．

![image](https://user-images.githubusercontent.com/8305330/155836991-0ec624ee-5141-4dcb-9451-7d1d6b9a8385.png)

インストールをクリックし，下記画像のメッセージが表示されれば完了です．

![image](https://user-images.githubusercontent.com/8305330/155837022-cde00c30-ee15-4d1c-ac85-11dc61c3f036.png)

## ☑️ Todo

[Projects](https://github.com/xyzyxJP/GakujoGUI-WPF/projects/2)

## 📜 License

[MIT License](https://opensource.org/licenses/MIT)
