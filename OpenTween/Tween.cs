﻿// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
// 
// This file is part of OpenTween.
// 
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
// 
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General public License
// for more details. 
// 
// You should have received a copy of the GNU General public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

#nullable enable

//コンパイル後コマンド
//"c:\Program Files\Microsoft.NET\SDK\v2.0\Bin\sgen.exe" /f /a:"$(TargetPath)"
//"C:\Program Files\Microsoft Visual Studio 8\SDK\v2.0\Bin\sgen.exe" /f /a:"$(TargetPath)"

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTween.Api;
using OpenTween.Api.DataModel;
using OpenTween.Connection;
using OpenTween.Models;
using OpenTween.OpenTweenCustomControl;
using OpenTween.Setting;
using OpenTween.Thumbnail;

namespace OpenTween
{
    public partial class TweenMain : OTBaseForm
    {
        //各種設定

        /// <summary>画面サイズ</summary>
        private Size _mySize;

        /// <summary>画面位置</summary>
        private Point _myLoc;

        /// <summary>区切り位置</summary>
        private int _mySpDis;

        /// <summary>発言欄区切り位置</summary>
        private int _mySpDis2;

        /// <summary>プレビュー区切り位置</summary>
        private int _mySpDis3;

        /// <summary>アイコンサイズ</summary>
        /// <remarks>
        /// 現在は16、24、48の3種類。将来直接数字指定可能とする
        /// 注：24x24の場合に26と指定しているのはMSゴシック系フォントのための仕様
        /// </remarks>
        private int _iconSz;

        private bool _iconCol; // 1列表示の時true（48サイズのとき）

        //雑多なフラグ類
        private bool _initial; // true:起動時処理中
        private bool _initialLayout = true;
        private bool _ignoreConfigSave; // true:起動時処理中

        /// <summary>タブドラッグ中フラグ（DoDragDropを実行するかの判定用）</summary>
        private bool _tabDrag;

        private TabPage? _beforeSelectedTab; // タブが削除されたときに前回選択されていたときのタブを選択する為に保持
        private Point _tabMouseDownPoint;

        /// <summary>右クリックしたタブの名前（Tabコントロール機能不足対応）</summary>
        private string? _rclickTabName;

        private readonly object _syncObject = new object(); // ロック用

        private const string detailHtmlFormatHeaderMono = 
            "<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=8\">"
            + "<style type=\"text/css\"><!-- "
            + "body, p, pre {margin: 0;} "
            + "pre {font-family: \"%FONT_FAMILY%\", sans-serif; font-size: %FONT_SIZE%pt; background-color:rgb(%BG_COLOR%); word-wrap: break-word; color:rgb(%FONT_COLOR%);} "
            + "a:link, a:visited, a:active, a:hover {color:rgb(%LINK_COLOR%); } "
            + "img.emoji {width: 1em; height: 1em; margin: 0 .05em 0 .1em; vertical-align: -0.1em; border: none;} "
            + ".quote-tweet {border: 1px solid #ccc; margin: 1em; padding: 0.5em;} "
            + ".quote-tweet.reply {border-color: #f33;} "
            + ".quote-tweet-link {color: inherit !important; text-decoration: none;}"
            + "--></style>"
            + "</head><body><pre>";
        private const string detailHtmlFormatFooterMono = "</pre></body></html>";
        private const string detailHtmlFormatHeaderColor = 
            "<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=8\">"
            + "<style type=\"text/css\"><!-- "
            + "body, p, pre {margin: 0;} "
            + "body {font-family: \"%FONT_FAMILY%\", sans-serif; font-size: %FONT_SIZE%pt; background-color:rgb(%BG_COLOR%); margin: 0; word-wrap: break-word; color:rgb(%FONT_COLOR%);} "
            + "a:link, a:visited, a:active, a:hover {color:rgb(%LINK_COLOR%); } "
            + "img.emoji {width: 1em; height: 1em; margin: 0 .05em 0 .1em; vertical-align: -0.1em; border: none;} "
            + ".quote-tweet {border: 1px solid #ccc; margin: 1em; padding: 0.5em;} "
            + ".quote-tweet.reply {border-color: rgb(%BG_REPLY_COLOR%);} "
            + ".quote-tweet-link {color: inherit !important; text-decoration: none;}"
            + "--></style>"
            + "</head><body><p>";
        private const string detailHtmlFormatFooterColor = "</p></body></html>";
        private string detailHtmlFormatHeader = null!;
        private string detailHtmlFormatFooter = null!;

        private bool _myStatusError = false;
        private bool _myStatusOnline = false;
        private bool soundfileListup = false;
        private FormWindowState _formWindowState = FormWindowState.Normal; // フォームの状態保存用 通知領域からアイコンをクリックして復帰した際に使用する

        //twitter解析部
        private readonly TwitterApi twitterApi = new TwitterApi();
        private Twitter tw = null!;

        //Growl呼び出し部
        private readonly GrowlHelper gh = new GrowlHelper(ApplicationSettings.ApplicationName);

        //サブ画面インスタンス

        /// <summary>検索画面インスタンス</summary>
        internal SearchWordDialog SearchDialog = new SearchWordDialog();

        private readonly OpenURL UrlDialog = new OpenURL();

        /// <summary>@id補助</summary>
        public AtIdSupplement AtIdSupl = null!;

        /// <summary>Hashtag補助</summary>
        public AtIdSupplement HashSupl = null!;

        public HashtagManage HashMgr = null!;
        private EventViewerDialog evtDialog = null!;

        //表示フォント、色、アイコン

        /// <summary>未読用フォント</summary>
        private Font _fntUnread = null!;

        /// <summary>未読用文字色</summary>
        private Color _clUnread;

        /// <summary>既読用フォント</summary>
        private Font _fntReaded = null!;

        /// <summary>既読用文字色</summary>
        private Color _clReaded;

        /// <summary>Fav用文字色</summary>
        private Color _clFav;

        /// <summary>片思い用文字色</summary>
        private Color _clOWL;

        /// <summary>Retweet用文字色</summary>
        private Color _clRetweet;

        /// <summary>選択中の行用文字色</summary>
        private readonly Color _clHighLight = Color.FromKnownColor(KnownColor.HighlightText);

        /// <summary>発言詳細部用フォント</summary>
        private Font _fntDetail = null!;

        /// <summary>発言詳細部用色</summary>
        private Color _clDetail;

        /// <summary>発言詳細部用リンク文字色</summary>
        private Color _clDetailLink;

        /// <summary>発言詳細部用背景色</summary>
        private Color _clDetailBackcolor;

        /// <summary>自分の発言用背景色</summary>
        private Color _clSelf;

        /// <summary>自分宛返信用背景色</summary>
        private Color _clAtSelf;

        /// <summary>選択発言者の他の発言用背景色</summary>
        private Color _clTarget;

        /// <summary>選択発言中の返信先用背景色</summary>
        private Color _clAtTarget;

        /// <summary>選択発言者への返信発言用背景色</summary>
        private Color _clAtFromTarget;

        /// <summary>選択発言の唯一＠先</summary>
        private Color _clAtTo;

        /// <summary>リスト部通常発言背景色</summary>
        private Color _clListBackcolor;

        /// <summary>入力欄背景色</summary>
        private Color _clInputBackcolor;

        /// <summary>入力欄文字色</summary>
        private Color _clInputFont;

        /// <summary>入力欄フォント</summary>
        private Font _fntInputFont = null!;

        /// <summary>アイコン画像リスト</summary>
        private ImageCache IconCache = null!;

        /// <summary>タスクトレイアイコン：通常時 (At.ico)</summary>
        private Icon NIconAt = null!;

        /// <summary>タスクトレイアイコン：通信エラー時 (AtRed.ico)</summary>
        private Icon NIconAtRed = null!;

        /// <summary>タスクトレイアイコン：オフライン時 (AtSmoke.ico)</summary>
        private Icon NIconAtSmoke = null!;

        /// <summary>タスクトレイアイコン：更新中 (Refresh.ico)</summary>
        private Icon[] NIconRefresh = new Icon[4];

        /// <summary>未読のあるタブ用アイコン (Tab.ico)</summary>
        private Icon TabIcon = null!;

        /// <summary>画面左上のアイコン (Main.ico)</summary>
        private Icon MainIcon = null!;

        private Icon ReplyIcon = null!;
        private Icon ReplyIconBlink = null!;

        private readonly ImageList _listViewImageList = new ImageList();    //ListViewItemの高さ変更用

        private PostClass? _anchorPost;
        private bool _anchorFlag;        //true:関連発言移動中（関連移動以外のオペレーションをするとfalseへ。trueだとリスト背景色をアンカー発言選択中として描画）

        /// <summary>発言履歴</summary>
        private readonly List<StatusTextHistory> _history = new List<StatusTextHistory>();

        /// <summary>発言履歴カレントインデックス</summary>
        private int _hisIdx;

        //発言投稿時のAPI引数（発言編集時に設定。手書きreplyでは設定されない）

        /// <summary>リプライ先のステータスID・スクリーン名</summary>
        private (long StatusId, string ScreenName)? inReplyTo = null;

        //時速表示用
        private readonly List<DateTimeUtc> _postTimestamps = new List<DateTimeUtc>();
        private readonly List<DateTimeUtc> _favTimestamps = new List<DateTimeUtc>();

        // 以下DrawItem関連
        private readonly SolidBrush _brsHighLight = new SolidBrush(Color.FromKnownColor(KnownColor.Highlight));
        private SolidBrush _brsBackColorMine = null!;
        private SolidBrush _brsBackColorAt = null!;
        private SolidBrush _brsBackColorYou = null!;
        private SolidBrush _brsBackColorAtYou = null!;
        private SolidBrush _brsBackColorAtFromTarget = null!;
        private SolidBrush _brsBackColorAtTo = null!;
        private SolidBrush _brsBackColorNone = null!;

        /// <summary>Listにフォーカスないときの選択行の背景色</summary>
        private readonly SolidBrush _brsDeactiveSelection = new SolidBrush(Color.FromKnownColor(KnownColor.ButtonFace));

        private readonly StringFormat sfTab = new StringFormat();

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        private TabInformations _statuses = null!;

        /// <summary>
        /// 現在表示している発言一覧の <see cref="ListView"/> に対するキャッシュ
        /// </summary>
        /// <remarks>
        /// キャッシュクリアのために null が代入されることがあるため、
        /// 使用する場合には <see cref="_listItemCache"/> に対して直接メソッド等を呼び出さずに
        /// 一旦ローカル変数に代入してから参照すること。
        /// </remarks>
        private ListViewItemCache? _listItemCache = null;

        internal class ListViewItemCache
        {
            /// <summary>アイテムをキャッシュする対象の <see cref="ListView"/></summary>
            public ListView TargetList { get; set; } = null!;

            /// <summary>キャッシュする範囲の開始インデックス</summary>
            public int StartIndex { get; set; }

            /// <summary>キャッシュする範囲の終了インデックス</summary>
            public int EndIndex { get; set; }

            /// <summary>キャッシュされた範囲に対応する <see cref="ListViewItem"/> と <see cref="PostClass"/> の組</summary>
            public (ListViewItem, PostClass)[] Cache { get; set; } = null!;

            /// <summary>キャッシュされたアイテムの件数</summary>
            public int Count
                => this.EndIndex - this.StartIndex + 1;

            /// <summary>指定されたインデックスがキャッシュの範囲内であるか判定します</summary>
            /// <returns><paramref name="index"/> がキャッシュの範囲内であれば true、それ以外は false</returns>
            public bool Contains(int index)
                => index >= this.StartIndex && index <= this.EndIndex;

            /// <summary>指定されたインデックスの範囲が全てキャッシュの範囲内であるか判定します</summary>
            /// <returns><paramref name="rangeStart"/> から <paramref name="rangeEnd"/> の範囲が全てキャッシュの範囲内であれば true、それ以外は false</returns>
            public bool IsSupersetOf(int rangeStart, int rangeEnd)
                => rangeStart >= this.StartIndex && rangeEnd <= this.EndIndex;

            /// <summary>指定されたインデックスの <see cref="ListViewItem"/> と <see cref="PostClass"/> をキャッシュから取得することを試みます</summary>
            /// <returns>取得に成功すれば true、それ以外は false</returns>
            public bool TryGetValue(int index, [NotNullWhen(true)] out ListViewItem? item, [NotNullWhen(true)] out PostClass? post)
            {
                if (this.Contains(index))
                {
                    (item, post) = this.Cache[index - this.StartIndex];
                    return true;
                }
                else
                {
                    item = null;
                    post = null;
                    return false;
                }
            }
        }

        private bool _isColumnChanged = false;

        private const int MAX_WORKER_THREADS = 20;
        private readonly SemaphoreSlim workerSemaphore = new SemaphoreSlim(MAX_WORKER_THREADS);
        private readonly CancellationTokenSource workerCts = new CancellationTokenSource();
        private readonly IProgress<string> workerProgress = null!;

        private int UnreadCounter = -1;
        private int UnreadAtCounter = -1;

        private readonly string[] ColumnOrgText = new string[9];
        private readonly string[] ColumnText = new string[9];

        private bool _DoFavRetweetFlags = false;

        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        private readonly TimelineScheduler timelineScheduler = new TimelineScheduler();
        private ThrottlingTimer RefreshThrottlingTimer = null!;
        private ThrottlingTimer selectionDebouncer = null!;
        private ThrottlingTimer saveConfigDebouncer = null!;

        private string recommendedStatusFooter = null!;
        private bool urlMultibyteSplit = false;
        private bool preventSmsCommand = true;

        // URL短縮のUndo用
        private struct urlUndo
        {
            public string Before;
            public string After;
        }

        private List<urlUndo>? urlUndoBuffer = null;

        private readonly struct ReplyChain
        {
            public readonly long OriginalId;
            public readonly long InReplyToId;
            public readonly TabModel OriginalTab;

            public ReplyChain(long originalId, long inReplyToId, TabModel originalTab)
            {
                this.OriginalId = originalId;
                this.InReplyToId = inReplyToId;
                this.OriginalTab = originalTab;
            }
        }

        /// <summary>[, ]でのリプライ移動の履歴</summary>
        private Stack<ReplyChain>? replyChains;

        /// <summary>ポスト選択履歴</summary>
        private readonly Stack<(TabModel, PostClass?)> selectPostChains = new Stack<(TabModel, PostClass?)>();

        public TabModel CurrentTab
            => this._statuses.SelectedTab;

        public string CurrentTabName
            => this._statuses.SelectedTabName;

        public TabPage CurrentTabPage
            => this.ListTab.TabPages[this._statuses.Tabs.IndexOf(this.CurrentTabName)];

        public DetailsListView CurrentListView
            => (DetailsListView)this.CurrentTabPage.Tag;

        public PostClass? CurrentPost
            => this.CurrentTab.SelectedPost;

        /// <summary>検索処理タイプ</summary>
        internal enum SEARCHTYPE
        {
            DialogSearch,
            NextSearch,
            PrevSearch,
        }

        private class StatusTextHistory
        {
            public string status = "";
            public (long StatusId, string ScreenName)? inReplyTo = null;

            /// <summary>画像投稿サービス名</summary>
            public string imageService = "";

            public IMediaItem[]? mediaItems = null;
            public StatusTextHistory()
            {
            }
            public StatusTextHistory(string status, (long StatusId, string ScreenName)? inReplyTo)
            {
                this.status = status;
                this.inReplyTo = inReplyTo;
            }
        }

        private void TweenMain_Activated(object sender, EventArgs e)
        {
            //画面がアクティブになったら、発言欄の背景色戻す
            if (StatusText.Focused)
            {
                this.StatusText_Enter(this.StatusText, System.EventArgs.Empty);
            }
        }

        private bool disposed = false;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (this.disposed)
                return;

            if (disposing)
            {
                this.components?.Dispose();

                //後始末
                SearchDialog.Dispose();
                UrlDialog.Dispose();
                NIconAt?.Dispose();
                NIconAtRed?.Dispose();
                NIconAtSmoke?.Dispose();
                foreach (var iconRefresh in this.NIconRefresh)
                {
                    iconRefresh?.Dispose();
                }
                TabIcon?.Dispose();
                MainIcon?.Dispose();
                ReplyIcon?.Dispose();
                ReplyIconBlink?.Dispose();
                _listViewImageList.Dispose();
                _brsHighLight.Dispose();
                _brsBackColorMine?.Dispose();
                _brsBackColorAt?.Dispose();
                _brsBackColorYou?.Dispose();
                _brsBackColorAtYou?.Dispose();
                _brsBackColorAtFromTarget?.Dispose();
                _brsBackColorAtTo?.Dispose();
                _brsBackColorNone?.Dispose();
                _brsDeactiveSelection?.Dispose();
                //sf.Dispose();
                sfTab.Dispose();

                this.workerCts.Cancel();

                if (IconCache != null)
                {
                    this.IconCache.CancelAsync();
                    this.IconCache.Dispose();
                }

                this.thumbnailTokenSource?.Dispose();

                this.tw.Dispose();
                this.twitterApi.Dispose();
                this._hookGlobalHotkey.Dispose();
            }

            // 終了時にRemoveHandlerしておかないとメモリリークする
            // http://msdn.microsoft.com/ja-jp/library/microsoft.win32.systemevents.powermodechanged.aspx
            Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            Microsoft.Win32.SystemEvents.TimeChanged -= SystemEvents_TimeChanged;

            this.disposed = true;
        }

        private void LoadIcons()
        {
            // Icons フォルダ以下のアイコンを読み込み（着せ替えアイコン対応）
            var iconsDir = Path.Combine(Application.StartupPath, "Icons");

            // ウィンドウ左上のアイコン
            var iconMain = this.LoadIcon(Path.Combine(iconsDir, "MIcon.ico"));

            // タブ見出し未読表示アイコン
            var iconTab = this.LoadIcon(Path.Combine(iconsDir, "Tab.ico"));

            // タスクトレイ: 通常時アイコン
            var iconAt = this.LoadIcon(Path.Combine(iconsDir, "At.ico"));

            // タスクトレイ: エラー時アイコン
            var iconAtRed = this.LoadIcon(Path.Combine(iconsDir, "AtRed.ico"));

            // タスクトレイ: オフライン時アイコン
            var iconAtSmoke = this.LoadIcon(Path.Combine(iconsDir, "AtSmoke.ico"));

            // タスクトレイ: Reply通知アイコン (最大2枚でアニメーション可能)
            var iconReply = this.LoadIcon(Path.Combine(iconsDir, "Reply.ico"));
            var iconReplyBlink = this.LoadIcon(Path.Combine(iconsDir, "ReplyBlink.ico"));

            // タスクトレイ: 更新中アイコン (最大4枚でアニメーション可能)
            var iconRefresh1 = this.LoadIcon(Path.Combine(iconsDir, "Refresh.ico"));
            var iconRefresh2 = this.LoadIcon(Path.Combine(iconsDir, "Refresh2.ico"));
            var iconRefresh3 = this.LoadIcon(Path.Combine(iconsDir, "Refresh3.ico"));
            var iconRefresh4 = this.LoadIcon(Path.Combine(iconsDir, "Refresh4.ico"));

            // 読み込んだアイコンを設定 (不足するアイコンはデフォルトのものを設定)

            this.MainIcon = iconMain ?? Properties.Resources.MIcon;
            this.TabIcon = iconTab ?? Properties.Resources.TabIcon;
            this.NIconAt = iconAt ?? iconMain ?? Properties.Resources.At;
            this.NIconAtRed = iconAtRed ?? Properties.Resources.AtRed;
            this.NIconAtSmoke = iconAtSmoke ?? Properties.Resources.AtSmoke;

            if (iconReply != null && iconReplyBlink != null)
            {
                this.ReplyIcon = iconReply;
                this.ReplyIconBlink = iconReplyBlink;
            }
            else
            {
                this.ReplyIcon = iconReply ?? iconReplyBlink ?? Properties.Resources.Reply;
                this.ReplyIconBlink = this.NIconAt;
            }

            if (iconRefresh1 == null)
            {
                this.NIconRefresh = new[] {
                    Properties.Resources.Refresh, Properties.Resources.Refresh2,
                    Properties.Resources.Refresh3, Properties.Resources.Refresh4,
                };
            }
            else if (iconRefresh2 == null)
            {
                this.NIconRefresh = new[] { iconRefresh1 };
            }
            else if (iconRefresh3 == null)
            {
                this.NIconRefresh = new[] { iconRefresh1, iconRefresh2 };
            }
            else if (iconRefresh4 == null)
            {
                this.NIconRefresh = new[] { iconRefresh1, iconRefresh2, iconRefresh3 };
            }
            else // iconRefresh1 から iconRefresh4 まで全て揃っている
            {
                this.NIconRefresh = new[] { iconRefresh1, iconRefresh2, iconRefresh3, iconRefresh4 };
            }
        }

        private Icon? LoadIcon(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                return new Icon(filePath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void InitColumns(ListView list, bool startup)
        {
            this.InitColumnText();

            ColumnHeader[]? columns = null;
            try
            {
                if (this._iconCol)
                {
                    columns = new[]
                    {
                        new ColumnHeader(), // アイコン
                        new ColumnHeader(), // 本文
                    };

                    columns[0].Text = this.ColumnText[0];
                    columns[1].Text = this.ColumnText[2];

                    if (startup)
                    {
                        var widthScaleFactor = this.CurrentAutoScaleDimensions.Width / SettingManager.Local.ScaleDimension.Width;

                        columns[0].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width1);
                        columns[1].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width3);
                        columns[0].DisplayIndex = 0;
                        columns[1].DisplayIndex = 1;
                    }
                    else
                    {
                        var idx = 0;
                        foreach (var curListColumn in this.CurrentListView.Columns.Cast<ColumnHeader>())
                        {
                            columns[idx].Width = curListColumn.Width;
                            columns[idx].DisplayIndex = curListColumn.DisplayIndex;
                            idx++;
                        }
                    }
                }
                else
                {
                    columns = new[]
                    {
                        new ColumnHeader(), // アイコン
                        new ColumnHeader(), // ニックネーム
                        new ColumnHeader(), // 本文
                        new ColumnHeader(), // 日付
                        new ColumnHeader(), // ユーザID
                        new ColumnHeader(), // 未読
                        new ColumnHeader(), // マーク＆プロテクト
                        new ColumnHeader(), // ソース
                    };

                    foreach (var i in Enumerable.Range(0, columns.Length))
                        columns[i].Text = this.ColumnText[i];

                    if (startup)
                    {
                        var widthScaleFactor = this.CurrentAutoScaleDimensions.Width / SettingManager.Local.ScaleDimension.Width;

                        columns[0].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width1);
                        columns[1].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width2);
                        columns[2].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width3);
                        columns[3].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width4);
                        columns[4].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width5);
                        columns[5].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width6);
                        columns[6].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width7);
                        columns[7].Width = ScaleBy(widthScaleFactor, SettingManager.Local.Width8);

                        var displayIndex = new[] {
                            SettingManager.Local.DisplayIndex1, SettingManager.Local.DisplayIndex2,
                            SettingManager.Local.DisplayIndex3, SettingManager.Local.DisplayIndex4,
                            SettingManager.Local.DisplayIndex5, SettingManager.Local.DisplayIndex6,
                            SettingManager.Local.DisplayIndex7, SettingManager.Local.DisplayIndex8
                        };

                        foreach (var i in Enumerable.Range(0, displayIndex.Length))
                        {
                            columns[i].DisplayIndex = displayIndex[i];
                        }
                    }
                    else
                    {
                        var idx = 0;
                        foreach (var curListColumn in this.CurrentListView.Columns.Cast<ColumnHeader>())
                        {
                            columns[idx].Width = curListColumn.Width;
                            columns[idx].DisplayIndex = curListColumn.DisplayIndex;
                            idx++;
                        }
                    }
                }

                list.Columns.AddRange(columns);

                columns = null;
            }
            finally
            {
                if (columns != null)
                {
                    foreach (var column in columns)
                        column?.Dispose();
                }
            }
        }

        private void InitColumnText()
        {
            ColumnText[0] = "";
            ColumnText[1] = Properties.Resources.AddNewTabText2;
            ColumnText[2] = Properties.Resources.AddNewTabText3;
            ColumnText[3] = Properties.Resources.AddNewTabText4_2;
            ColumnText[4] = Properties.Resources.AddNewTabText5;
            ColumnText[5] = "";
            ColumnText[6] = "";
            ColumnText[7] = "Source";

            ColumnOrgText[0] = "";
            ColumnOrgText[1] = Properties.Resources.AddNewTabText2;
            ColumnOrgText[2] = Properties.Resources.AddNewTabText3;
            ColumnOrgText[3] = Properties.Resources.AddNewTabText4_2;
            ColumnOrgText[4] = Properties.Resources.AddNewTabText5;
            ColumnOrgText[5] = "";
            ColumnOrgText[6] = "";
            ColumnOrgText[7] = "Source";

            var c = this._statuses.SortMode switch
            {
                ComparerMode.Nickname => 1, // ニックネーム
                ComparerMode.Data => 2, // 本文
                ComparerMode.Id => 3, // 時刻=発言Id
                ComparerMode.Name => 4, // 名前
                ComparerMode.Source => 7, // Source
                _ => 0,
            };

            if (_iconCol)
            {
                if (_statuses.SortOrder == SortOrder.Descending)
                {
                    // U+25BE BLACK DOWN-POINTING SMALL TRIANGLE
                    ColumnText[2] = ColumnOrgText[2] + "▾";
                }
                else
                {
                    // U+25B4 BLACK UP-POINTING SMALL TRIANGLE
                    ColumnText[2] = ColumnOrgText[2] + "▴";
                }
            }
            else
            {
                if (_statuses.SortOrder == SortOrder.Descending)
                {
                    // U+25BE BLACK DOWN-POINTING SMALL TRIANGLE
                    ColumnText[c] = ColumnOrgText[c] + "▾";
                }
                else
                {
                    // U+25B4 BLACK UP-POINTING SMALL TRIANGLE
                    ColumnText[c] = ColumnOrgText[c] + "▴";
                }
            }
        }

        private void InitializeTraceFrag()
        {
#if DEBUG
            TraceOutToolStripMenuItem.Checked = true;
            MyCommon.TraceFlag = true;
#endif
            if (!MyCommon.FileVersion.EndsWith("0", StringComparison.Ordinal))
            {
                TraceOutToolStripMenuItem.Checked = true;
                MyCommon.TraceFlag = true;
            }
        }

        private void TweenMain_Load(object sender, EventArgs e)
        {
            _ignoreConfigSave = true;
            this.Visible = false;

            if (MyApplication.StartupOptions.ContainsKey("d"))
                MyCommon.TraceFlag = true;

            InitializeTraceFrag();

            Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            Regex.CacheSize = 100;

            //発言保持クラス
            _statuses = TabInformations.GetInstance();

            //アイコン設定
            LoadIcons();
            this.Icon = MainIcon;              //メインフォーム（TweenMain）
            NotifyIcon1.Icon = NIconAt;      //タスクトレイ
            TabImage.Images.Add(TabIcon);    //タブ見出し

            //<<<<<<<<<設定関連>>>>>>>>>
            ////設定読み出し
            LoadConfig();

            // 現在の DPI と設定保存時の DPI との比を取得する
            var configScaleFactor = SettingManager.Local.GetConfigScaleFactor(this.CurrentAutoScaleDimensions);

            // UIフォント設定
            var fontUIGlobal = SettingManager.Local.FontUIGlobal;
            if (fontUIGlobal != null)
            {
                OTBaseForm.GlobalFont = fontUIGlobal;
                this.Font = fontUIGlobal;
            }

            //不正値チェック
            if (!MyApplication.StartupOptions.ContainsKey("nolimit"))
            {
                if (SettingManager.Common.TimelinePeriod < 15 && SettingManager.Common.TimelinePeriod > 0)
                    SettingManager.Common.TimelinePeriod = 15;

                if (SettingManager.Common.ReplyPeriod < 15 && SettingManager.Common.ReplyPeriod > 0)
                    SettingManager.Common.ReplyPeriod = 15;

                if (SettingManager.Common.DMPeriod < 15 && SettingManager.Common.DMPeriod > 0)
                    SettingManager.Common.DMPeriod = 15;

                if (SettingManager.Common.PubSearchPeriod < 30 && SettingManager.Common.PubSearchPeriod > 0)
                    SettingManager.Common.PubSearchPeriod = 30;

                if (SettingManager.Common.UserTimelinePeriod < 15 && SettingManager.Common.UserTimelinePeriod > 0)
                    SettingManager.Common.UserTimelinePeriod = 15;

                if (SettingManager.Common.ListsPeriod < 15 && SettingManager.Common.ListsPeriod > 0)
                    SettingManager.Common.ListsPeriod = 15;
            }

            if (!Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.Timeline, SettingManager.Common.CountApi))
                SettingManager.Common.CountApi = 60;
            if (!Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.Reply, SettingManager.Common.CountApiReply))
                SettingManager.Common.CountApiReply = 40;

            if (SettingManager.Common.MoreCountApi != 0 && !Twitter.VerifyMoreApiResultCount(SettingManager.Common.MoreCountApi))
                SettingManager.Common.MoreCountApi = 200;
            if (SettingManager.Common.FirstCountApi != 0 && !Twitter.VerifyFirstApiResultCount(SettingManager.Common.FirstCountApi))
                SettingManager.Common.FirstCountApi = 100;

            if (SettingManager.Common.FavoritesCountApi != 0 && !Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.Favorites, SettingManager.Common.FavoritesCountApi))
                SettingManager.Common.FavoritesCountApi = 40;
            if (SettingManager.Common.ListCountApi != 0 && !Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.List, SettingManager.Common.ListCountApi))
                SettingManager.Common.ListCountApi = 100;
            if (SettingManager.Common.SearchCountApi != 0 && !Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.PublicSearch, SettingManager.Common.SearchCountApi))
                SettingManager.Common.SearchCountApi = 100;
            if (SettingManager.Common.UserTimelineCountApi != 0 && !Twitter.VerifyApiResultCount(MyCommon.WORKERTYPE.UserTimeline, SettingManager.Common.UserTimelineCountApi))
                SettingManager.Common.UserTimelineCountApi = 20;

            //廃止サービスが選択されていた場合ux.nuへ読み替え
            if (SettingManager.Common.AutoShortUrlFirst < 0)
                SettingManager.Common.AutoShortUrlFirst = MyCommon.UrlConverter.Uxnu;

            TwitterApiConnection.RestApiHost = SettingManager.Common.TwitterApiHost;
            this.tw = new Twitter(this.twitterApi);

            //認証関連
            if (MyCommon.IsNullOrEmpty(SettingManager.Common.Token)) SettingManager.Common.UserName = "";
            tw.Initialize(SettingManager.Common.Token, SettingManager.Common.TokenSecret, SettingManager.Common.UserName, SettingManager.Common.UserId);

            _initial = true;

            Networking.Initialize();

            var saveRequired = false;
            var firstRun = false;

            //ユーザー名、パスワードが未設定なら設定画面を表示（初回起動時など）
            if (MyCommon.IsNullOrEmpty(tw.Username))
            {
                saveRequired = true;
                firstRun = true;

                //設定せずにキャンセルされたか、設定されたが依然ユーザー名が未設定ならプログラム終了
                if (ShowSettingDialog(showTaskbarIcon: true) != DialogResult.OK ||
                    MyCommon.IsNullOrEmpty(tw.Username))
                {
                    Application.Exit();  //強制終了
                    return;
                }
            }

            //Twitter用通信クラス初期化
            Networking.DefaultTimeout = TimeSpan.FromSeconds(SettingManager.Common.DefaultTimeOut);
            Networking.UploadImageTimeout = TimeSpan.FromSeconds(SettingManager.Common.UploadImageTimeout);
            Networking.SetWebProxy(SettingManager.Local.ProxyType,
                SettingManager.Local.ProxyAddress, SettingManager.Local.ProxyPort,
                SettingManager.Local.ProxyUser, SettingManager.Local.ProxyPassword);
            Networking.ForceIPv4 = SettingManager.Common.ForceIPv4;

            TwitterApiConnection.RestApiHost = SettingManager.Common.TwitterApiHost;
            tw.RestrictFavCheck = SettingManager.Common.RestrictFavCheck;
            tw.ReadOwnPost = SettingManager.Common.ReadOwnPost;
            tw.TrackWord = SettingManager.Common.TrackWord;
            TrackToolStripMenuItem.Checked = !MyCommon.IsNullOrEmpty(tw.TrackWord);
            tw.AllAtReply = SettingManager.Common.AllAtReply;
            AllrepliesToolStripMenuItem.Checked = tw.AllAtReply;
            ShortUrl.Instance.DisableExpanding = !SettingManager.Common.TinyUrlResolve;
            ShortUrl.Instance.BitlyAccessToken = SettingManager.Common.BitlyAccessToken;
            ShortUrl.Instance.BitlyId = SettingManager.Common.BilyUser;
            ShortUrl.Instance.BitlyKey = SettingManager.Common.BitlyPwd;

            // アクセストークンが有効であるか確認する
            // ここが Twitter API への最初のアクセスになるようにすること
            try
            {
                this.tw.VerifyCredentials();
            }
            catch (WebApiException ex)
            {
                MessageBox.Show(this, string.Format(Properties.Resources.StartupAuthError_Text, ex.Message),
                    ApplicationSettings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            //サムネイル関連の初期化
            //プロキシ設定等の通信まわりの初期化が済んでから処理する
            ThumbnailGenerator.InitializeGenerator();

            var imgazyobizinet = ThumbnailGenerator.ImgAzyobuziNetInstance;
            imgazyobizinet.Enabled = SettingManager.Common.EnableImgAzyobuziNet;
            imgazyobizinet.DisabledInDM = SettingManager.Common.ImgAzyobuziNetDisabledInDM;

            Thumbnail.Services.TonTwitterCom.GetApiConnection = () => this.twitterApi.Connection;

            //画像投稿サービス
            ImageSelector.Initialize(tw, this.tw.Configuration, SettingManager.Common.UseImageServiceName, SettingManager.Common.UseImageService);

            //ハッシュタグ/@id関連
            AtIdSupl = new AtIdSupplement(SettingManager.AtIdList.AtIdList, "@");
            HashSupl = new AtIdSupplement(SettingManager.Common.HashTags, "#");
            HashMgr = new HashtagManage(HashSupl,
                                    SettingManager.Common.HashTags.ToArray(),
                                    SettingManager.Common.HashSelected,
                                    SettingManager.Common.HashIsPermanent,
                                    SettingManager.Common.HashIsHead,
                                    SettingManager.Common.HashIsNotAddToAtReply);
            if (!MyCommon.IsNullOrEmpty(HashMgr.UseHash) && HashMgr.IsPermanent) HashStripSplitButton.Text = HashMgr.UseHash;

            //アイコンリスト作成
            this.IconCache = new ImageCache();
            this.tweetDetailsView.IconCache = this.IconCache;

            //フォント＆文字色＆背景色保持
            _fntUnread = SettingManager.Local.FontUnread;
            _clUnread = SettingManager.Local.ColorUnread;
            _fntReaded = SettingManager.Local.FontRead;
            _clReaded = SettingManager.Local.ColorRead;
            _clFav = SettingManager.Local.ColorFav;
            _clOWL = SettingManager.Local.ColorOWL;
            _clRetweet = SettingManager.Local.ColorRetweet;
            _fntDetail = SettingManager.Local.FontDetail;
            _clDetail = SettingManager.Local.ColorDetail;
            _clDetailLink = SettingManager.Local.ColorDetailLink;
            _clDetailBackcolor = SettingManager.Local.ColorDetailBackcolor;
            _clSelf = SettingManager.Local.ColorSelf;
            _clAtSelf = SettingManager.Local.ColorAtSelf;
            _clTarget = SettingManager.Local.ColorTarget;
            _clAtTarget = SettingManager.Local.ColorAtTarget;
            _clAtFromTarget = SettingManager.Local.ColorAtFromTarget;
            _clAtTo = SettingManager.Local.ColorAtTo;
            _clListBackcolor = SettingManager.Local.ColorListBackcolor;
            _clInputBackcolor = SettingManager.Local.ColorInputBackcolor;
            _clInputFont = SettingManager.Local.ColorInputFont;
            _fntInputFont = SettingManager.Local.FontInputFont;

            _brsBackColorMine = new SolidBrush(_clSelf);
            _brsBackColorAt = new SolidBrush(_clAtSelf);
            _brsBackColorYou = new SolidBrush(_clTarget);
            _brsBackColorAtYou = new SolidBrush(_clAtTarget);
            _brsBackColorAtFromTarget = new SolidBrush(_clAtFromTarget);
            _brsBackColorAtTo = new SolidBrush(_clAtTo);
            _brsBackColorNone = new SolidBrush(_clListBackcolor);

            // StringFormatオブジェクトへの事前設定
            sfTab.Alignment = StringAlignment.Center;
            sfTab.LineAlignment = StringAlignment.Center;

            InitDetailHtmlFormat();

            this.recommendedStatusFooter = " [TWNv" + Regex.Replace(MyCommon.FileVersion.Replace(".", ""), "^0*", "") + "]";

            _history.Add(new StatusTextHistory());
            _hisIdx = 0;
            this.inReplyTo = null;

            //各種ダイアログ設定
            SearchDialog.Owner = this;
            UrlDialog.Owner = this;

            //新着バルーン通知のチェック状態設定
            NewPostPopMenuItem.Checked = SettingManager.Common.NewAllPop;
            this.NotifyFileMenuItem.Checked = NewPostPopMenuItem.Checked;

            //新着取得時のリストスクロールをするか。trueならスクロールしない
            ListLockMenuItem.Checked = SettingManager.Common.ListLock;
            this.LockListFileMenuItem.Checked = SettingManager.Common.ListLock;
            //サウンド再生（タブ別設定より優先）
            this.PlaySoundMenuItem.Checked = SettingManager.Common.PlaySound;
            this.PlaySoundFileMenuItem.Checked = SettingManager.Common.PlaySound;

            //ウィンドウ設定
            this.ClientSize = ScaleBy(configScaleFactor, SettingManager.Local.FormSize);
            _mySize = this.ClientSize; // サイズ保持（最小化・最大化されたまま終了した場合の対応用）
            _myLoc = SettingManager.Local.FormLocation;
            //タイトルバー領域
            if (this.WindowState != FormWindowState.Minimized)
            {
                var tbarRect = new Rectangle(this._myLoc, new Size(_mySize.Width, SystemInformation.CaptionHeight));
                var outOfScreen = true;
                if (Screen.AllScreens.Length == 1)    //ハングするとの報告
                {
                    foreach (var scr in Screen.AllScreens)
                    {
                        if (!Rectangle.Intersect(tbarRect, scr.Bounds).IsEmpty)
                        {
                            outOfScreen = false;
                            break;
                        }
                    }

                    if (outOfScreen)
                        this._myLoc = new Point(0, 0);
                }
                this.DesktopLocation = this._myLoc;
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
            _mySpDis = ScaleBy(configScaleFactor.Height, SettingManager.Local.SplitterDistance);
            _mySpDis2 = ScaleBy(configScaleFactor.Height, SettingManager.Local.StatusTextHeight);
            if (SettingManager.Local.PreviewDistance == -1)
            {
                _mySpDis3 = _mySize.Width - ScaleBy(this.CurrentScaleFactor.Width, 150);
                if (_mySpDis3 < 1) _mySpDis3 = ScaleBy(this.CurrentScaleFactor.Width, 50);
                SettingManager.Local.PreviewDistance = _mySpDis3;
            }
            else
            {
                _mySpDis3 = ScaleBy(configScaleFactor.Width, SettingManager.Local.PreviewDistance);
            }
            this.PlaySoundMenuItem.Checked = SettingManager.Common.PlaySound;
            this.PlaySoundFileMenuItem.Checked = SettingManager.Common.PlaySound;
            //入力欄
            StatusText.Font = _fntInputFont;
            StatusText.ForeColor = _clInputFont;

            // SplitContainer2.Panel2MinSize を一行表示の入力欄の高さに合わせる (MS UI Gothic 12pt (96dpi) の場合は 19px)
            this.StatusText.Multiline = false; // SettingManager.Local.StatusMultiline の設定は後で反映される
            this.SplitContainer2.Panel2MinSize = this.StatusText.Height;

            // 必要であれば、発言一覧と発言詳細部・入力欄の上下を入れ替える
            SplitContainer1.IsPanelInverted = !SettingManager.Common.StatusAreaAtBottom;

            //全新着通知のチェック状態により、Reply＆DMの新着通知有効無効切り替え（タブ別設定にするため削除予定）
            if (SettingManager.Common.UnreadManage == false)
            {
                ReadedStripMenuItem.Enabled = false;
                UnreadStripMenuItem.Enabled = false;
            }

            //リンク先URL表示部の初期化（画面左下）
            StatusLabelUrl.Text = "";
            //状態表示部の初期化（画面右下）
            StatusLabel.Text = "";
            StatusLabel.AutoToolTip = false;
            StatusLabel.ToolTipText = "";
            //文字カウンタ初期化
            lblLen.Text = this.GetRestStatusCount(this.FormatStatusTextExtended("")).ToString();

            this.JumpReadOpMenuItem.ShortcutKeyDisplayString = "Space";
            CopySTOTMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            CopyURLMenuItem.ShortcutKeyDisplayString = "Ctrl+Shift+C";
            CopyUserIdStripMenuItem.ShortcutKeyDisplayString = "Shift+Alt+C";

            // SourceLinkLabel のテキストが SplitContainer2.Panel2.AccessibleName にセットされるのを防ぐ
            // （タブオーダー順で SourceLinkLabel の次にある PostBrowser が TabStop = false となっているため、
            // さらに次のコントロールである SplitContainer2.Panel2 の AccessibleName がデフォルトで SourceLinkLabel のテキストになってしまう)
            this.SplitContainer2.Panel2.AccessibleName = "";

            ////////////////////////////////////////////////////////////////////////////////
            var sortOrder = (SortOrder)SettingManager.Common.SortOrder;
            var mode = ComparerMode.Id;
            switch (SettingManager.Common.SortColumn)
            {
                case 0:    //0:アイコン,5:未読マーク,6:プロテクト・フィルターマーク
                case 5:
                case 6:
                    //ソートしない
                    mode = ComparerMode.Id;  //Idソートに読み替え
                    break;
                case 1:  //ニックネーム
                    mode = ComparerMode.Nickname;
                    break;
                case 2:  //本文
                    mode = ComparerMode.Data;
                    break;
                case 3:  //時刻=発言Id
                    mode = ComparerMode.Id;
                    break;
                case 4:  //名前
                    mode = ComparerMode.Name;
                    break;
                case 7:  //Source
                    mode = ComparerMode.Source;
                    break;
            }
            _statuses.SetSortMode(mode, sortOrder);
            ////////////////////////////////////////////////////////////////////////////////

            ApplyListViewIconSize(SettingManager.Common.IconSize);

            //<<<<<<<<タブ関連>>>>>>>

            //デフォルトタブの存在チェック、ない場合には追加
            if (this._statuses.GetTabByType<HomeTabModel>() == null)
                this._statuses.AddTab(new HomeTabModel());

            if (this._statuses.GetTabByType<MentionsTabModel>() == null)
                this._statuses.AddTab(new MentionsTabModel());

            if (this._statuses.GetTabByType<DirectMessagesTabModel>() == null)
                this._statuses.AddTab(new DirectMessagesTabModel());

            if (this._statuses.GetTabByType<FavoritesTabModel>() == null)
                this._statuses.AddTab(new FavoritesTabModel());

            if (this._statuses.MuteTab == null)
                this._statuses.AddTab(new MuteTabModel());

            foreach (var tab in _statuses.Tabs)
            {
                if (!AddNewTab(tab, startup: true))
                    throw new TabException(Properties.Resources.TweenMain_LoadText1);
            }

            this._statuses.SelectTab(this.ListTab.SelectedTab.Text);

            // タブの位置を調整する
            SetTabAlignment();

            MyCommon.TwitterApiInfo.AccessLimitUpdated += TwitterApiStatus_AccessLimitUpdated;
            Microsoft.Win32.SystemEvents.TimeChanged += SystemEvents_TimeChanged;

            if (SettingManager.Common.TabIconDisp)
            {
                ListTab.DrawMode = TabDrawMode.Normal;
            }
            else
            {
                ListTab.DrawMode = TabDrawMode.OwnerDrawFixed;
                ListTab.DrawItem += ListTab_DrawItem;
                ListTab.ImageList = null;
            }

            if (SettingManager.Common.HotkeyEnabled)
            {
                //////グローバルホットキーの登録
                var modKey = HookGlobalHotkey.ModKeys.None;
                if ((SettingManager.Common.HotkeyModifier & Keys.Alt) == Keys.Alt)
                    modKey |= HookGlobalHotkey.ModKeys.Alt;
                if ((SettingManager.Common.HotkeyModifier & Keys.Control) == Keys.Control)
                    modKey |= HookGlobalHotkey.ModKeys.Ctrl;
                if ((SettingManager.Common.HotkeyModifier & Keys.Shift) == Keys.Shift)
                    modKey |= HookGlobalHotkey.ModKeys.Shift;
                if ((SettingManager.Common.HotkeyModifier & Keys.LWin) == Keys.LWin)
                    modKey |= HookGlobalHotkey.ModKeys.Win;

                _hookGlobalHotkey.RegisterOriginalHotkey(SettingManager.Common.HotkeyKey, SettingManager.Common.HotkeyValue, modKey);
            }

            if (SettingManager.Common.IsUseNotifyGrowl)
                gh.RegisterGrowl();

            StatusLabel.Text = Properties.Resources.Form1_LoadText1;       //画面右下の状態表示を変更

            SetMainWindowTitle();
            SetNotifyIconText();

            if (!SettingManager.Common.MinimizeToTray || this.WindowState != FormWindowState.Minimized)
            {
                this.Visible = true;
            }

            //タイマー設定

            this.timelineScheduler.UpdateHome = () => this.InvokeAsync(() => this.RefreshTabAsync<HomeTabModel>());
            this.timelineScheduler.UpdateMention = () => this.InvokeAsync(() => this.RefreshTabAsync<MentionsTabModel>());
            this.timelineScheduler.UpdateDm = () => this.InvokeAsync(() => this.RefreshTabAsync<DirectMessagesTabModel>());
            this.timelineScheduler.UpdatePublicSearch = () => this.InvokeAsync(() => this.RefreshTabAsync<PublicSearchTabModel>());
            this.timelineScheduler.UpdateUser = () => this.InvokeAsync(() => this.RefreshTabAsync<UserTimelineTabModel>());
            this.timelineScheduler.UpdateList = () => this.InvokeAsync(() => this.RefreshTabAsync<ListTimelineTabModel>());
            this.timelineScheduler.UpdateConfig = () => this.InvokeAsync(() => Task.WhenAll(new[]
            {
                this.doGetFollowersMenu(),
                this.RefreshBlockIdsAsync(),
                this.RefreshMuteUserIdsAsync(),
                this.RefreshNoRetweetIdsAsync(),
                this.RefreshTwitterConfigurationAsync(),
            }));
            this.RefreshTimelineScheduler();

            var streamingRefreshInterval = TimeSpan.FromSeconds(SettingManager.Common.UserstreamPeriod);
            this.RefreshThrottlingTimer = ThrottlingTimer.Throttle(() => this.InvokeAsync(() => this.RefreshTimeline()), streamingRefreshInterval);
            this.selectionDebouncer = ThrottlingTimer.Debounce(() => this.InvokeAsync(() => this.UpdateSelectedPost()), TimeSpan.FromMilliseconds(100), leading: true);
            this.saveConfigDebouncer = ThrottlingTimer.Debounce(() => this.InvokeAsync(() => this.SaveConfigsAll(ifModified: true)), TimeSpan.FromSeconds(1));

            //更新中アイコンアニメーション間隔
            TimerRefreshIcon.Interval = 200;
            TimerRefreshIcon.Enabled = false;

            _ignoreConfigSave = false;
            this.TweenMain_Resize(this, EventArgs.Empty);
            if (saveRequired) SaveConfigsAll(false);

            foreach (var ua in SettingManager.Common.UserAccounts)
            {
                if (ua.UserId == 0 && ua.Username.Equals(tw.Username, StringComparison.InvariantCultureIgnoreCase))
                {
                    ua.UserId = tw.UserId;
                    break;
                }
            }

            if (firstRun)
            {
                // 初回起動時だけ右下のメニューを目立たせる
                HashStripSplitButton.ShowDropDown();
            }
        }

        private void InitDetailHtmlFormat()
        {
            if (SettingManager.Common.IsMonospace)
            {
                detailHtmlFormatHeader = detailHtmlFormatHeaderMono;
                detailHtmlFormatFooter = detailHtmlFormatFooterMono;
            }
            else
            {
                detailHtmlFormatHeader = detailHtmlFormatHeaderColor;
                detailHtmlFormatFooter = detailHtmlFormatFooterColor;
            }

            detailHtmlFormatHeader = detailHtmlFormatHeader
                    .Replace("%FONT_FAMILY%", _fntDetail.Name)
                    .Replace("%FONT_SIZE%", _fntDetail.Size.ToString())
                    .Replace("%FONT_COLOR%", $"{_clDetail.R},{_clDetail.G},{_clDetail.B}")
                    .Replace("%LINK_COLOR%", $"{_clDetailLink.R},{_clDetailLink.G},{_clDetailLink.B}")
                    .Replace("%BG_COLOR%", $"{_clDetailBackcolor.R},{_clDetailBackcolor.G},{_clDetailBackcolor.B}")
                    .Replace("%BG_REPLY_COLOR%", $"{_clAtTo.R}, {_clAtTo.G}, {_clAtTo.B}");
        }

        private void ListTab_DrawItem(object sender, DrawItemEventArgs e)
        {
            string txt;
            try
            {
                txt = this._statuses.Tabs[e.Index].TabName;
            }
            catch (Exception)
            {
                return;
            }

            e.Graphics.FillRectangle(System.Drawing.SystemBrushes.Control, e.Bounds);
            if (e.State == DrawItemState.Selected)
            {
                e.DrawFocusRectangle();
            }
            Brush fore;
            try
            {
                if (_statuses.Tabs[txt].UnreadCount > 0)
                    fore = Brushes.Red;
                else
                    fore = System.Drawing.SystemBrushes.ControlText;
            }
            catch (Exception)
            {
                fore = System.Drawing.SystemBrushes.ControlText;
            }
            e.Graphics.DrawString(txt, e.Font, fore, e.Bounds, sfTab);
        }

        private void LoadConfig()
        {
            SettingManager.Local = SettingManager.Local;

            // v1.2.4 以前の設定には ScaleDimension の項目がないため、現在の DPI と同じとして扱う
            if (SettingManager.Local.ScaleDimension.IsEmpty)
                SettingManager.Local.ScaleDimension = this.CurrentAutoScaleDimensions;

            var tabSettings = SettingManager.Tabs;
            foreach (var tabSetting in tabSettings.Tabs)
            {
                TabModel tab;
                switch (tabSetting.TabType)
                {
                    case MyCommon.TabUsageType.Home:
                        tab = new HomeTabModel(tabSetting.TabName);
                        break;
                    case MyCommon.TabUsageType.Mentions:
                        tab = new MentionsTabModel(tabSetting.TabName);
                        break;
                    case MyCommon.TabUsageType.DirectMessage:
                        tab = new DirectMessagesTabModel(tabSetting.TabName);
                        break;
                    case MyCommon.TabUsageType.Favorites:
                        tab = new FavoritesTabModel(tabSetting.TabName);
                        break;
                    case MyCommon.TabUsageType.UserDefined:
                        tab = new FilterTabModel(tabSetting.TabName);
                        break;
                    case MyCommon.TabUsageType.UserTimeline:
                        tab = new UserTimelineTabModel(tabSetting.TabName, tabSetting.User!);
                        break;
                    case MyCommon.TabUsageType.PublicSearch:
                        tab = new PublicSearchTabModel(tabSetting.TabName)
                        {
                            SearchWords = tabSetting.SearchWords,
                            SearchLang = tabSetting.SearchLang,
                        };
                        break;
                    case MyCommon.TabUsageType.Lists:
                        tab = new ListTimelineTabModel(tabSetting.TabName, tabSetting.ListInfo!);
                        break;
                    case MyCommon.TabUsageType.Mute:
                        tab = new MuteTabModel(tabSetting.TabName);
                        break;
                    default:
                        continue;
                }

                tab.UnreadManage = tabSetting.UnreadManage;
                tab.Protected = tabSetting.Protected;
                tab.Notify = tabSetting.Notify;
                tab.SoundFile = tabSetting.SoundFile;

                if (tab.IsDistributableTabType)
                {
                    var filterTab = (FilterTabModel)tab;
                    filterTab.FilterArray = tabSetting.FilterArray;
                    filterTab.FilterModified = false;
                }

                if (this._statuses.ContainsTab(tab.TabName))
                    tab.TabName = this._statuses.MakeTabName("MyTab");

                this._statuses.AddTab(tab);
            }
            if (_statuses.Tabs.Count == 0)
            {
                _statuses.AddTab(new HomeTabModel());
                _statuses.AddTab(new MentionsTabModel());
                _statuses.AddTab(new DirectMessagesTabModel());
                _statuses.AddTab(new FavoritesTabModel());
            }
        }

        private void TimerInterval_Changed(object sender, IntervalChangedEventArgs e)
        {
            if (e.UserStream)
            {
                var interval = TimeSpan.FromSeconds(SettingManager.Common.UserstreamPeriod);
                var newTimer = ThrottlingTimer.Throttle(() => this.InvokeAsync(() => this.RefreshTimeline()), interval);
                var oldTimer = Interlocked.Exchange(ref this.RefreshThrottlingTimer, newTimer);
                oldTimer.Dispose();
            }

            this.RefreshTimelineScheduler();
        }

        private void RefreshTimelineScheduler()
        {
            static TimeSpan intervalSecondsOrDisabled(int seconds)
                => seconds == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);

            this.timelineScheduler.UpdateIntervalHome = intervalSecondsOrDisabled(SettingManager.Common.TimelinePeriod);
            this.timelineScheduler.UpdateIntervalMention = intervalSecondsOrDisabled(SettingManager.Common.ReplyPeriod);
            this.timelineScheduler.UpdateIntervalDm = intervalSecondsOrDisabled(SettingManager.Common.DMPeriod);
            this.timelineScheduler.UpdateIntervalPublicSearch = intervalSecondsOrDisabled(SettingManager.Common.PubSearchPeriod);
            this.timelineScheduler.UpdateIntervalUser = intervalSecondsOrDisabled(SettingManager.Common.UserTimelinePeriod);
            this.timelineScheduler.UpdateIntervalList = intervalSecondsOrDisabled(SettingManager.Common.ListsPeriod);
            this.timelineScheduler.UpdateIntervalConfig = TimeSpan.FromHours(6);
            this.timelineScheduler.UpdateAfterSystemResume = TimeSpan.FromSeconds(30);

            this.timelineScheduler.RefreshSchedule();
        }

        private void MarkSettingCommonModified()
        {
            if (this.saveConfigDebouncer == null)
                return;

            this.ModifySettingCommon = true;
            this.saveConfigDebouncer.Call();
        }

        private void MarkSettingLocalModified()
        {
            if (this.saveConfigDebouncer == null)
                return;

            this.ModifySettingLocal = true;
            this.saveConfigDebouncer.Call();
        }

        internal void MarkSettingAtIdModified()
        {
            if (this.saveConfigDebouncer == null)
                return;

            this.ModifySettingAtId = true;
            this.saveConfigDebouncer.Call();
        }

        private void RefreshTimeline()
        {
            var curTabModel = this.CurrentTab;
            var curListView = this.CurrentListView;

            // 現在表示中のタブのスクロール位置を退避
            var curListScroll = this.SaveListViewScroll(curListView, curTabModel);

            // 各タブのリスト上の選択位置などを退避
            var listSelections = this.SaveListViewSelection();

            //更新確定
            int addCount;
            addCount = _statuses.SubmitUpdate(out var soundFile, out var notifyPosts,
                out var newMentionOrDm, out var isDelete);

            if (MyCommon._endingFlag) return;

            // リストに反映＆選択状態復元
            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var tabPage = this.ListTab.TabPages[index];
                var listView = (DetailsListView)tabPage.Tag;

                if (listView.VirtualListSize != tab.AllCount || isDelete)
                {
                    using (ControlTransaction.Update(listView))
                    {
                        if (listView == curListView)
                            this.PurgeListViewItemCache();

                        try
                        {
                            // リスト件数更新
                            listView.VirtualListSize = tab.AllCount;
                        }
                        catch (NullReferenceException ex)
                        {
                            // WinForms 内部で ListView.set_TopItem が発生させている例外
                            // https://ja.osdn.net/ticket/browse.php?group_id=6526&tid=36588
                            MyCommon.TraceOut(ex, $"TabType: {tab.TabType}, Count: {tab.AllCount}, ListSize: {listView.VirtualListSize}");
                        }

                        // 選択位置などを復元
                        this.RestoreListViewSelection(listView, tab, listSelections[tab.TabName]);
                    }
                }
            }

            if (addCount > 0)
            {
                if (SettingManager.Common.TabIconDisp)
                {
                    foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                    {
                        var tabPage = this.ListTab.TabPages[index];
                        if (tab.UnreadCount > 0 && tabPage.ImageIndex != 0)
                            tabPage.ImageIndex = 0; // 未読アイコン
                    }
                }
                else
                {
                    this.ListTab.Refresh();
                }
            }

            // スクロール位置を復元
            this.RestoreListViewScroll(curListView, curTabModel, curListScroll);

            //新着通知
            NotifyNewPosts(notifyPosts, soundFile, addCount, newMentionOrDm);

            SetMainWindowTitle();
            if (!StatusLabelUrl.Text.StartsWith("http", StringComparison.Ordinal)) SetStatusLabelUrl();

            HashSupl.AddRangeItem(tw.GetHashList());

        }

        internal struct ListViewScroll
        {
            public ScrollLockMode ScrollLockMode { get; set; }
            public long? TopItemStatusId { get; set; }
        }

        internal enum ScrollLockMode
        {
            /// <summary>固定しない</summary>
            None,

            /// <summary>最上部に固定する</summary>
            FixedToTop,

            /// <summary>最下部に固定する</summary>
            FixedToBottom,

            /// <summary><see cref="ListViewScroll.TopItemStatusId"/> の位置に固定する</summary>
            FixedToItem,
        }

        /// <summary>
        /// <see cref="ListView"/> のスクロール位置に関する情報を <see cref="ListViewScroll"/> として返します
        /// </summary>
        private ListViewScroll SaveListViewScroll(DetailsListView listView, TabModel tab)
        {
            var listScroll = new ListViewScroll
            {
                ScrollLockMode = this.GetScrollLockMode(listView),
            };

            if (listScroll.ScrollLockMode == ScrollLockMode.FixedToItem)
            {
                var topItemIndex = listView.TopItem?.Index ?? -1;
                if (topItemIndex != -1 && topItemIndex < tab.AllCount)
                    listScroll.TopItemStatusId = tab.GetStatusIdAt(topItemIndex);
            }

            return listScroll;
        }

        private ScrollLockMode GetScrollLockMode(DetailsListView listView)
        {
            if (this._statuses.SortMode == ComparerMode.Id)
            {
                if (this._statuses.SortOrder == SortOrder.Ascending)
                {
                    // Id昇順
                    if (this.ListLockMenuItem.Checked)
                        return ScrollLockMode.None;

                    // 最下行が表示されていたら、最下行へ強制スクロール。最下行が表示されていなかったら制御しない

                    // 一番下に表示されているアイテム
                    var bottomItem = listView.GetItemAt(0, listView.ClientSize.Height - 1);
                    if (bottomItem == null || bottomItem.Index == listView.VirtualListSize - 1)
                        return ScrollLockMode.FixedToBottom;
                    else
                        return ScrollLockMode.None;
                }
                else
                {
                    // Id降順
                    if (this.ListLockMenuItem.Checked)
                        return ScrollLockMode.FixedToItem;

                    // 最上行が表示されていたら、制御しない。最上行が表示されていなかったら、現在表示位置へ強制スクロール
                    var topItem = listView.TopItem;
                    if (topItem == null || topItem.Index == 0)
                        return ScrollLockMode.FixedToTop;
                    else
                        return ScrollLockMode.FixedToItem;
                }
            }
            else
            {
                return ScrollLockMode.FixedToItem;
            }
        }

        internal struct ListViewSelection
        {
            public long[]? SelectedStatusIds { get; set; }
            public long? SelectionMarkStatusId { get; set; }
            public long? FocusedStatusId { get; set; }
        }

        /// <summary>
        /// <see cref="ListView"/> の選択状態を <see cref="ListViewSelection"/> として返します
        /// </summary>
        private IReadOnlyDictionary<string, ListViewSelection> SaveListViewSelection()
        {
            var listsDict = new Dictionary<string, ListViewSelection>();

            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var listView = (DetailsListView)this.ListTab.TabPages[index].Tag;
                listsDict[tab.TabName] = this.SaveListViewSelection(listView, tab);
            }

            return listsDict;
        }

        /// <summary>
        /// <see cref="ListView"/> の選択状態を <see cref="ListViewSelection"/> として返します
        /// </summary>
        private ListViewSelection SaveListViewSelection(DetailsListView listView, TabModel tab)
        {
            if (listView.VirtualListSize == 0)
            {
                return new ListViewSelection
                {
                    SelectedStatusIds = Array.Empty<long>(),
                    SelectionMarkStatusId = null,
                    FocusedStatusId = null,
                };
            }

            return new ListViewSelection
            {
                SelectedStatusIds = tab.SelectedStatusIds,
                FocusedStatusId = this.GetFocusedStatusId(listView, tab),
                SelectionMarkStatusId = this.GetSelectionMarkStatusId(listView, tab),
            };
        }

        private long? GetFocusedStatusId(DetailsListView listView, TabModel tab)
        {
            var index = listView.FocusedItem?.Index ?? -1;

            return index != -1 && index < tab.AllCount ? tab.GetStatusIdAt(index) : (long?)null;
        }

        private long? GetSelectionMarkStatusId(DetailsListView listView, TabModel tab)
        {
            var index = listView.SelectionMark;

            return index != -1 && index < tab.AllCount ? tab.GetStatusIdAt(index) : (long?)null;
        }

        /// <summary>
        /// <see cref="SaveListViewScroll"/> によって保存されたスクロール位置を復元します
        /// </summary>
        private void RestoreListViewScroll(DetailsListView listView, TabModel tab, ListViewScroll listScroll)
        {
            if (listView.VirtualListSize == 0)
                return;

            switch (listScroll.ScrollLockMode)
            {
                case ScrollLockMode.FixedToTop:
                    listView.EnsureVisible(0);
                    break;
                case ScrollLockMode.FixedToBottom:
                    listView.EnsureVisible(listView.VirtualListSize - 1);
                    break;
                case ScrollLockMode.FixedToItem:
                    var topIndex = listScroll.TopItemStatusId != null ? tab.IndexOf(listScroll.TopItemStatusId.Value) : -1;
                    if (topIndex != -1)
                    {
                        var topItem = listView.Items[topIndex];
                        try
                        {
                            listView.TopItem = topItem;
                        }
                        catch (NullReferenceException)
                        {
                            listView.EnsureVisible(listView.VirtualListSize - 1);
                            listView.EnsureVisible(topIndex);
                        }
                    }
                    break;
                case ScrollLockMode.None:
                default:
                    break;
            }
        }

        /// <summary>
        /// <see cref="SaveListViewSelection"/> によって保存された選択状態を復元します
        /// </summary>
        private void RestoreListViewSelection(DetailsListView listView, TabModel tab, ListViewSelection listSelection)
        {
            // status_id から ListView 上のインデックスに変換
            int[]? selectedIndices = null;
            if (listSelection.SelectedStatusIds != null)
                selectedIndices = tab.IndexOf(listSelection.SelectedStatusIds).Where(x => x != -1).ToArray();

            var focusedIndex = -1;
            if (listSelection.FocusedStatusId != null)
                focusedIndex = tab.IndexOf(listSelection.FocusedStatusId.Value);

            var selectionMarkIndex = -1;
            if (listSelection.SelectionMarkStatusId != null)
                selectionMarkIndex = tab.IndexOf(listSelection.SelectionMarkStatusId.Value);

            this.SelectListItem(listView, selectedIndices, focusedIndex, selectionMarkIndex);
        }

        private bool BalloonRequired()
        {
            var ev = new Twitter.FormattedEvent
            {
                Eventtype = MyCommon.EVENTTYPE.None,
            };

            return BalloonRequired(ev);
        }

        private bool IsEventNotifyAsEventType(MyCommon.EVENTTYPE type)
        {
            if (type == MyCommon.EVENTTYPE.None)
                return true;

            if (!SettingManager.Common.EventNotifyEnabled)
                return false;

            return SettingManager.Common.EventNotifyFlag.HasFlag(type);
        }

        private bool IsMyEventNotityAsEventType(Twitter.FormattedEvent ev)
        {
            if (!ev.IsMe)
                return true;

            return SettingManager.Common.IsMyEventNotifyFlag.HasFlag(ev.Eventtype);
        }

        private bool BalloonRequired(Twitter.FormattedEvent ev)
        {
            if (this._initial)
                return false;

            if (NativeMethods.IsScreenSaverRunning())
                return false;

            // 「新着通知」が無効
            if (!this.NewPostPopMenuItem.Checked)
            {
                // 「新着通知が無効でもイベントを通知する」にも該当しない
                if (!SettingManager.Common.ForceEventNotify || ev.Eventtype == MyCommon.EVENTTYPE.None)
                    return false;
            }

            // 「画面最小化・アイコン時のみバルーンを表示する」が有効
            if (SettingManager.Common.LimitBalloon)
            {
                if (this.WindowState != FormWindowState.Minimized && this.Visible && Form.ActiveForm != null)
                    return false;
            }

            return this.IsEventNotifyAsEventType(ev.Eventtype) && this.IsMyEventNotityAsEventType(ev);
        }

        private void NotifyNewPosts(PostClass[] notifyPosts, string soundFile, int addCount, bool newMentions)
        {
            if (SettingManager.Common.ReadOwnPost)
            {
                if (notifyPosts != null && notifyPosts.Length > 0 && notifyPosts.All(x => x.UserId == tw.UserId))
                    return;
            }

            //新着通知
            if (BalloonRequired())
            {
                if (notifyPosts != null && notifyPosts.Length > 0)
                {
                    //Growlは一個ずつばらして通知。ただし、3ポスト以上あるときはまとめる
                    if (SettingManager.Common.IsUseNotifyGrowl)
                    {
                        var sb = new StringBuilder();
                        var reply = false;
                        var dm = false;

                        foreach (var post in notifyPosts)
                        {
                            if (!(notifyPosts.Length > 3))
                            {
                                sb.Clear();
                                reply = false;
                                dm = false;
                            }
                            if (post.IsReply && !post.IsExcludeReply) reply = true;
                            if (post.IsDm) dm = true;
                            if (sb.Length > 0) sb.Append(System.Environment.NewLine);
                            switch (SettingManager.Common.NameBalloon)
                            {
                                case MyCommon.NameBalloonEnum.UserID:
                                    sb.Append(post.ScreenName).Append(" : ");
                                    break;
                                case MyCommon.NameBalloonEnum.NickName:
                                    sb.Append(post.Nickname).Append(" : ");
                                    break;
                            }
                            sb.Append(post.TextFromApi);
                            if (notifyPosts.Length > 3)
                            {
                                if (notifyPosts.Last() != post) continue;
                            }

                            var title = new StringBuilder();
                            GrowlHelper.NotifyType nt;
                            if (SettingManager.Common.DispUsername)
                            {
                                title.Append(tw.Username);
                                title.Append(" - ");
                            }

                            if (dm)
                            {
                                title.Append(ApplicationSettings.ApplicationName);
                                title.Append(" [DM] ");
                                title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                                nt = GrowlHelper.NotifyType.DirectMessage;
                            }
                            else if (reply)
                            {
                                title.Append(ApplicationSettings.ApplicationName);
                                title.Append(" [Reply!] ");
                                title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                                nt = GrowlHelper.NotifyType.Reply;
                            }
                            else
                            {
                                title.Append(ApplicationSettings.ApplicationName);
                                title.Append(" ");
                                title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                                nt = GrowlHelper.NotifyType.Notify;
                            }
                            var bText = sb.ToString();
                            if (MyCommon.IsNullOrEmpty(bText)) return;

                            var image = this.IconCache.TryGetFromCache(post.ImageUrl);
                            gh.Notify(nt, post.StatusId.ToString(), title.ToString(), bText, image?.Image, post.ImageUrl);
                        }
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        var reply = false;
                        var dm = false;
                        foreach (var post in notifyPosts)
                        {
                            if (post.IsReply && !post.IsExcludeReply) reply = true;
                            if (post.IsDm) dm = true;
                            if (sb.Length > 0) sb.Append(System.Environment.NewLine);
                            switch (SettingManager.Common.NameBalloon)
                            {
                                case MyCommon.NameBalloonEnum.UserID:
                                    sb.Append(post.ScreenName).Append(" : ");
                                    break;
                                case MyCommon.NameBalloonEnum.NickName:
                                    sb.Append(post.Nickname).Append(" : ");
                                    break;
                            }
                            sb.Append(post.TextFromApi);

                        }

                        var title = new StringBuilder();
                        ToolTipIcon ntIcon;
                        if (SettingManager.Common.DispUsername)
                        {
                            title.Append(tw.Username);
                            title.Append(" - ");
                        }

                        if (dm)
                        {
                            ntIcon = ToolTipIcon.Warning;
                            title.Append(ApplicationSettings.ApplicationName);
                            title.Append(" [DM] ");
                            title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                        }
                        else if (reply)
                        {
                            ntIcon = ToolTipIcon.Warning;
                            title.Append(ApplicationSettings.ApplicationName);
                            title.Append(" [Reply!] ");
                            title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                        }
                        else
                        {
                            ntIcon = ToolTipIcon.Info;
                            title.Append(ApplicationSettings.ApplicationName);
                            title.Append(" ");
                            title.AppendFormat(Properties.Resources.RefreshTimeline_NotifyText, addCount);
                        }
                        var bText = sb.ToString();
                        if (MyCommon.IsNullOrEmpty(bText)) return;

                        NotifyIcon1.BalloonTipTitle = title.ToString();
                        NotifyIcon1.BalloonTipText = bText;
                        NotifyIcon1.BalloonTipIcon = ntIcon;
                        NotifyIcon1.ShowBalloonTip(500);
                    }
                }
            }

            //サウンド再生
            if (!_initial && SettingManager.Common.PlaySound && !MyCommon.IsNullOrEmpty(soundFile))
            {
                try
                {
                    var dir = Application.StartupPath;
                    if (Directory.Exists(Path.Combine(dir, "Sounds")))
                    {
                        dir = Path.Combine(dir, "Sounds");
                    }
                    using var player = new SoundPlayer(Path.Combine(dir, soundFile));
                    player.Play();
                }
                catch (Exception)
                {
                }
            }

            //mentions新着時に画面ブリンク
            if (!_initial && SettingManager.Common.BlinkNewMentions && newMentions && Form.ActiveForm == null)
            {
                NativeMethods.FlashMyWindow(this.Handle, 3);
            }
        }

        private void MyList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var listView = this.CurrentListView;
            if (listView != sender)
                return;

            var indices = listView.SelectedIndices.Cast<int>().ToArray();
            this.CurrentTab.SelectPosts(indices);

            if (indices.Length != 1)
                return;

            var index = indices[0];
            if (index > listView.VirtualListSize - 1) return;

            this.PushSelectPostChain();

            var post = this.CurrentPost!;
            this._statuses.SetReadAllTab(post.StatusId, read: true);

            //キャッシュの書き換え
            ChangeCacheStyleRead(true, index); // 既読へ（フォント、文字色）

            this.ColorizeList();
            this.selectionDebouncer.Call();
        }

        private void ChangeCacheStyleRead(bool Read, int Index)
        {
            var tabInfo = this.CurrentTab;
            //Read:true=既読 false=未読
            //未読管理していなかったら既読として扱う
            if (!tabInfo.UnreadManage ||
               !SettingManager.Common.UnreadManage) Read = true;

            var listCache = this._listItemCache;
            if (listCache == null)
                return;

            // キャッシュに含まれていないアイテムは対象外
            if (!listCache.TryGetValue(Index, out var itm, out var post))
                return;

            ChangeItemStyleRead(Read, itm, post, (DetailsListView)listCache.TargetList);
        }

        private void ChangeItemStyleRead(bool Read, ListViewItem Item, PostClass Post, DetailsListView? DList)
        {
            Font fnt;
            string star;
            //フォント
            if (Read)
            {
                fnt = _fntReaded;
                star = "";
            }
            else
            {
                fnt = _fntUnread;
                star = "★";
            }
            if (Item.SubItems[5].Text != star)
                Item.SubItems[5].Text = star;

            //文字色
            Color cl;
            if (Post.IsFav)
                cl = _clFav;
            else if (Post.RetweetedId != null)
                cl = _clRetweet;
            else if (Post.IsOwl && (Post.IsDm || SettingManager.Common.OneWayLove))
                cl = _clOWL;
            else if (Read || !SettingManager.Common.UseUnreadStyle)
                cl = _clReaded;
            else
                cl = _clUnread;

            if (DList == null || Item.Index == -1)
            {
                Item.ForeColor = cl;
                if (SettingManager.Common.UseUnreadStyle)
                    Item.Font = fnt;
            }
            else
            {
                DList.Update();
                if (SettingManager.Common.UseUnreadStyle)
                    DList.ChangeItemFontAndColor(Item, cl, fnt);
                else
                    DList.ChangeItemForeColor(Item, cl);
            }
        }

        private void ColorizeList()
        {
            //Index:更新対象のListviewItem.Index。Colorを返す。
            //-1は全キャッシュ。Colorは返さない（ダミーを戻す）
            PostClass? _post;
            if (_anchorFlag)
                _post = _anchorPost;
            else
                _post = this.CurrentPost;

            if (_post == null) return;

            var listCache = this._listItemCache;
            if (listCache == null)
                return;

            var listView = (DetailsListView)listCache.TargetList;

            // ValidateRectが呼ばれる前に選択色などの描画を済ませておく
            listView.Update();

            foreach (var (listViewItem, cachedPost) in listCache.Cache)
            {
                var backColor = this.JudgeColor(_post, cachedPost);
                listView.ChangeItemBackColor(listViewItem, backColor);
            }
        }

        private void ColorizeList(ListViewItem Item, PostClass post)
        {
            //Index:更新対象のListviewItem.Index。Colorを返す。
            //-1は全キャッシュ。Colorは返さない（ダミーを戻す）
            PostClass? _post;
            if (_anchorFlag)
                _post = _anchorPost;
            else
                _post = this.CurrentPost;

            if (_post == null) return;

            if (Item.Index == -1)
                Item.BackColor = JudgeColor(_post, post);
            else
                this.CurrentListView.ChangeItemBackColor(Item, JudgeColor(_post, post));
        }

        private Color JudgeColor(PostClass BasePost, PostClass TargetPost)
        {
            Color cl;
            if (TargetPost.StatusId == BasePost.InReplyToStatusId)
                //@先
                cl = _clAtTo;
            else if (TargetPost.IsMe)
                //自分=発言者
                cl = _clSelf;
            else if (TargetPost.IsReply)
                //自分宛返信
                cl = _clAtSelf;
            else if (BasePost.ReplyToList.Any(x => x.UserId == TargetPost.UserId))
                //返信先
                cl = _clAtFromTarget;
            else if (TargetPost.ReplyToList.Any(x => x.UserId == BasePost.UserId))
                //その人への返信
                cl = _clAtTarget;
            else if (TargetPost.UserId == BasePost.UserId)
                //発言者
                cl = _clTarget;
            else
                //その他
                cl = _clListBackcolor;

            return cl;
        }

        private void StatusTextHistoryBack()
        {
            if (!string.IsNullOrWhiteSpace(this.StatusText.Text))
                this._history[_hisIdx] = new StatusTextHistory(this.StatusText.Text, this.inReplyTo);

            this._hisIdx -= 1;
            if (this._hisIdx < 0)
                this._hisIdx = 0;

            var historyItem = this._history[this._hisIdx];
            this.inReplyTo = historyItem.inReplyTo;
            this.StatusText.Text = historyItem.status;
            this.StatusText.SelectionStart = this.StatusText.Text.Length;
        }

        private void StatusTextHistoryForward()
        {
            if (!string.IsNullOrWhiteSpace(this.StatusText.Text))
                this._history[this._hisIdx] = new StatusTextHistory(this.StatusText.Text, this.inReplyTo);

            this._hisIdx += 1;
            if (this._hisIdx > this._history.Count - 1)
                this._hisIdx = this._history.Count - 1;

            var historyItem = this._history[this._hisIdx];
            this.inReplyTo = historyItem.inReplyTo;
            this.StatusText.Text = historyItem.status;
            this.StatusText.SelectionStart = this.StatusText.Text.Length;
        }

        private async void PostButton_Click(object sender, EventArgs e)
        {
            if (StatusText.Text.Trim().Length == 0)
            {
                if (!ImageSelector.Enabled)
                {
                    await this.DoRefresh();
                    return;
                }
            }

            var currentPost = this.CurrentPost;
            if (this.ExistCurrentPost && currentPost != null && StatusText.Text.Trim() == string.Format("RT @{0}: {1}", currentPost.ScreenName, currentPost.TextFromApi))
            {
                var rtResult = MessageBox.Show(string.Format(Properties.Resources.PostButton_Click1, Environment.NewLine),
                                                               "Retweet",
                                                               MessageBoxButtons.YesNoCancel,
                                                               MessageBoxIcon.Question);
                switch (rtResult)
                {
                    case DialogResult.Yes:
                        StatusText.Text = "";
                        await this.doReTweetOfficial(false);
                        return;
                    case DialogResult.Cancel:
                        return;
                }
            }

            if (TextContainsOnlyMentions(this.StatusText.Text))
            {
                var message = string.Format(Properties.Resources.PostConfirmText, this.StatusText.Text);
                var ret = MessageBox.Show(message, ApplicationSettings.ApplicationName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

                if (ret != DialogResult.OK)
                    return;
            }

            _history[_history.Count - 1] = new StatusTextHistory(StatusText.Text, this.inReplyTo);

            if (SettingManager.Common.Nicoms)
            {
                StatusText.SelectionStart = StatusText.Text.Length;
                await UrlConvertAsync(MyCommon.UrlConverter.Nicoms);
            }

            StatusText.SelectionStart = StatusText.Text.Length;
            CheckReplyTo(StatusText.Text);

            var status = new PostStatusParams();

            var statusTextCompat = this.FormatStatusText(this.StatusText.Text);
            if (this.GetRestStatusCount(statusTextCompat) >= 0)
            {
                // auto_populate_reply_metadata や attachment_url を使用しなくても 140 字以内に
                // 収まる場合はこれらのオプションを使用せずに投稿する
                status.Text = statusTextCompat;
                status.InReplyToStatusId = this.inReplyTo?.StatusId;
            }
            else
            {
                status.Text = this.FormatStatusTextExtended(this.StatusText.Text, out var autoPopulatedUserIds, out var attachmentUrl);
                status.InReplyToStatusId = this.inReplyTo?.StatusId;

                status.AttachmentUrl = attachmentUrl;

                // リプライ先がセットされていても autoPopulatedUserIds が空の場合は auto_populate_reply_metadata を有効にしない
                //  (非公式 RT の場合など)
                var replyToPost = this.inReplyTo != null ? this._statuses[this.inReplyTo.Value.StatusId] : null;
                if (replyToPost != null && autoPopulatedUserIds.Length != 0)
                {
                    status.AutoPopulateReplyMetadata = true;

                    // ReplyToList のうち autoPopulatedUserIds に含まれていないユーザー ID を抽出
                    status.ExcludeReplyUserIds = replyToPost.ReplyToList.Select(x => x.UserId).Except(autoPopulatedUserIds)
                        .ToArray();
                }
            }

            if (this.GetRestStatusCount(status.Text) < 0)
            {
                // 文字数制限を超えているが強制的に投稿するか
                var ret = MessageBox.Show(Properties.Resources.PostLengthOverMessage1, Properties.Resources.PostLengthOverMessage2, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (ret != DialogResult.OK)
                    return;
            }

            IMediaUploadService? uploadService = null;
            IMediaItem[]? uploadItems = null;
            if (ImageSelector.Visible)
            {
                //画像投稿
                if (!ImageSelector.TryGetSelectedMedia(out var serviceName, out uploadItems))
                    return;

                uploadService = this.ImageSelector.GetService(serviceName);
            }

            this.inReplyTo = null;
            StatusText.Text = "";
            _history.Add(new StatusTextHistory());
            _hisIdx = _history.Count - 1;
            if (!SettingManager.Common.FocusLockToStatusText)
                this.CurrentListView.Focus();
            urlUndoBuffer = null;
            UrlUndoToolStripMenuItem.Enabled = false;  //Undoをできないように設定

            //Google検索（試験実装）
            if (StatusText.Text.StartsWith("Google:", StringComparison.OrdinalIgnoreCase) && StatusText.Text.Trim().Length > 7)
            {
                var tmp = string.Format(Properties.Resources.SearchItem2Url, Uri.EscapeDataString(StatusText.Text.Substring(7)));
                await this.OpenUriInBrowserAsync(tmp);
            }

            await this.PostMessageAsync(status, uploadService, uploadItems);
        }

        private void EndToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MyCommon._endingFlag = true;
            this.Close();
        }

        private void TweenMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!SettingManager.Common.CloseToExit && e.CloseReason == CloseReason.UserClosing && MyCommon._endingFlag == false)
            {
                //_endingFlag=false:フォームの×ボタン
                e.Cancel = true;
                this.Visible = false;
            }
            else
            {
                _hookGlobalHotkey.UnregisterAllOriginalHotkey();
                _ignoreConfigSave = true;
                MyCommon._endingFlag = true;
                this.timelineScheduler.Enabled = false;
                TimerRefreshIcon.Enabled = false;
            }
        }

        private void NotifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            this.Visible = true;
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.BringToFront();
        }

        private static int errorCount = 0;

        private static bool CheckAccountValid()
        {
            if (Twitter.AccountState != MyCommon.ACCOUNT_STATE.Valid)
            {
                errorCount += 1;
                if (errorCount > 5)
                {
                    errorCount = 0;
                    Twitter.AccountState = MyCommon.ACCOUNT_STATE.Valid;
                    return true;
                }
                return false;
            }
            errorCount = 0;
            return true;
        }

        /// <summary>指定された型 <typeparamref name="T"/> に合致する全てのタブを更新します</summary>
        private Task RefreshTabAsync<T>() where T : TabModel
            => this.RefreshTabAsync<T>(backward: false);

        /// <summary>指定された型 <typeparamref name="T"/> に合致する全てのタブを更新します</summary>
        private Task RefreshTabAsync<T>(bool backward) where T : TabModel
        {
            var loadTasks =
                from tab in this._statuses.GetTabsByType<T>()
                select this.RefreshTabAsync(tab, backward);

            return Task.WhenAll(loadTasks);
        }

        /// <summary>指定されたタブ <paramref name="tab"/> を更新します</summary>
        private Task RefreshTabAsync(TabModel tab)
            => this.RefreshTabAsync(tab, backward: false);

        /// <summary>指定されたタブ <paramref name="tab"/> を更新します</summary>
        private async Task RefreshTabAsync(TabModel tab, bool backward)
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                this.RefreshTasktrayIcon();
                await Task.Run(() => tab.RefreshAsync(this.tw, backward, this._initial, this.workerProgress));
                this.RefreshTimeline();
            }
            catch (WebApiException ex)
            {
                this._myStatusError = true;
                var tabType = tab switch
                {
                    HomeTabModel _ => "GetTimeline",
                    MentionsTabModel _ => "GetTimeline",
                    DirectMessagesTabModel _ => "GetDirectMessage",
                    FavoritesTabModel _ => "GetFavorites",
                    PublicSearchTabModel _ => "GetSearch",
                    UserTimelineTabModel _ => "GetUserTimeline",
                    ListTimelineTabModel _ => "GetListStatus",
                    RelatedPostsTabModel _ => "GetRelatedTweets",
                    _ => tab.GetType().Name.Replace("Model", ""),
                };
                this.StatusLabel.Text = $"Err:{ex.Message}({tabType})";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task FavAddAsync(long statusId, TabModel tab)
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                var progress = new Progress<string>(x => this.StatusLabel.Text = x);

                this.RefreshTasktrayIcon();
                await this.FavAddAsyncInternal(progress, this.workerCts.Token, statusId, tab);
            }
            catch (WebApiException ex)
            {
                this._myStatusError = true;
                this.StatusLabel.Text = $"Err:{ex.Message}(PostFavAdd)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task FavAddAsyncInternal(IProgress<string> p, CancellationToken ct, long statusId, TabModel tab)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!CheckAccountValid())
                throw new WebApiException("Auth error. Check your account");

            if (!tab.Posts.TryGetValue(statusId, out var post))
                return;

            if (post.IsFav)
                return;

            await Task.Run(async () =>
            {
                p.Report(string.Format(Properties.Resources.GetTimelineWorker_RunWorkerCompletedText15, 0, 1, 0));

                try
                {
                    try
                    {
                        await this.twitterApi.FavoritesCreate(post.RetweetedId ?? post.StatusId)
                            .IgnoreResponse()
                            .ConfigureAwait(false);
                    }
                    catch (TwitterApiException ex)
                        when (ex.Errors.All(x => x.Code == TwitterErrorCode.AlreadyFavorited))
                    {
                        // エラーコード 139 のみの場合は成功と見なす
                    }

                    if (SettingManager.Common.RestrictFavCheck)
                    {
                        var status = await this.twitterApi.StatusesShow(post.RetweetedId ?? post.StatusId)
                            .ConfigureAwait(false);

                        if (status.Favorited != true)
                            throw new WebApiException("NG(Restricted?)");
                    }

                    this._favTimestamps.Add(DateTimeUtc.Now);

                    // TLでも取得済みならfav反映
                    if (this._statuses.Posts.TryGetValue(statusId, out var postTl))
                    {
                        postTl.IsFav = true;

                        var favTab = this._statuses.FavoriteTab;
                        favTab.AddPostQueue(postTl);
                    }

                    // 検索,リスト,UserTimeline,Relatedの各タブに反映
                    foreach (var tb in this._statuses.GetTabsInnerStorageType())
                    {
                        if (tb.Contains(statusId))
                            tb.Posts[statusId].IsFav = true;
                    }

                    p.Report(string.Format(Properties.Resources.GetTimelineWorker_RunWorkerCompletedText15, 1, 1, 0));
                }
                catch (WebApiException)
                {
                    p.Report(string.Format(Properties.Resources.GetTimelineWorker_RunWorkerCompletedText15, 1, 1, 1));
                    throw;
                }

                // 時速表示用
                var oneHour = DateTimeUtc.Now - TimeSpan.FromHours(1);
                foreach (var i in MyCommon.CountDown(this._favTimestamps.Count - 1, 0))
                {
                    if (this._favTimestamps[i] < oneHour)
                        this._favTimestamps.RemoveAt(i);
                }

                this._statuses.DistributePosts();
            });

            if (ct.IsCancellationRequested)
                return;

            this.RefreshTimeline();

            if (this.CurrentTabName == tab.TabName)
            {
                using (ControlTransaction.Update(this.CurrentListView))
                {
                    var idx = tab.IndexOf(statusId);
                    if (idx != -1)
                        this.ChangeCacheStyleRead(post.IsRead, idx);
                }

                var currentPost = this.CurrentPost;
                if (currentPost != null && statusId == currentPost.StatusId)
                    this.DispSelectedPost(true); // 選択アイテム再表示
            }
        }

        private async Task FavRemoveAsync(IReadOnlyList<long> statusIds, TabModel tab)
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                var progress = new Progress<string>(x => this.StatusLabel.Text = x);

                this.RefreshTasktrayIcon();
                await this.FavRemoveAsyncInternal(progress, this.workerCts.Token, statusIds, tab);
            }
            catch (WebApiException ex)
            {
                this._myStatusError = true;
                this.StatusLabel.Text = $"Err:{ex.Message}(PostFavRemove)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task FavRemoveAsyncInternal(IProgress<string> p, CancellationToken ct, IReadOnlyList<long> statusIds, TabModel tab)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!CheckAccountValid())
                throw new WebApiException("Auth error. Check your account");

            var successIds = new List<long>();

            await Task.Run(async () =>
            {
                //スレッド処理はしない
                var allCount = 0;
                var failedCount = 0;
                foreach (var statusId in statusIds)
                {
                    allCount++;

                    var post = tab.Posts[statusId];

                    p.Report(string.Format(Properties.Resources.GetTimelineWorker_RunWorkerCompletedText17, allCount, statusIds.Count, failedCount));

                    if (!post.IsFav)
                        continue;

                    try
                    {
                        await this.twitterApi.FavoritesDestroy(post.RetweetedId ?? post.StatusId)
                            .IgnoreResponse()
                            .ConfigureAwait(false);
                    }
                    catch (WebApiException)
                    {
                        failedCount++;
                        continue;
                    }

                    successIds.Add(statusId);
                    post.IsFav = false; // リスト再描画必要

                    if (this._statuses.Posts.TryGetValue(statusId, out var tabinfoPost))
                        tabinfoPost.IsFav = false;

                    // 検索,リスト,UserTimeline,Relatedの各タブに反映
                    foreach (var tb in this._statuses.GetTabsInnerStorageType())
                    {
                        if (tb.Contains(statusId))
                            tb.Posts[statusId].IsFav = false;
                    }
                }
            });

            if (ct.IsCancellationRequested)
                return;

            var favTab = this._statuses.FavoriteTab;
            foreach (var statusId in successIds)
            {
                // ツイートが削除された訳ではないので IsDeleted はセットしない
                favTab.EnqueueRemovePost(statusId, setIsDeleted: false);
            }

            this.RefreshTimeline();

            if (this.CurrentTabName == tab.TabName)
            {
                if (tab.TabType == MyCommon.TabUsageType.Favorites)
                {
                    // 色変えは不要
                }
                else
                {
                    using (ControlTransaction.Update(this.CurrentListView))
                    {
                        foreach (var statusId in successIds)
                        {
                            var idx = tab.IndexOf(statusId);
                            if (idx == -1)
                                continue;

                            var post = tab.Posts[statusId];
                            this.ChangeCacheStyleRead(post.IsRead, idx);
                        }
                    }

                    var currentPost = this.CurrentPost;
                    if (currentPost != null && successIds.Contains(currentPost.StatusId))
                        this.DispSelectedPost(true); // 選択アイテム再表示
                }
            }
        }

        private async Task PostMessageAsync(PostStatusParams postParams, IMediaUploadService? uploadService, IMediaItem[]? uploadItems)
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                var progress = new Progress<string>(x => this.StatusLabel.Text = x);

                this.RefreshTasktrayIcon();
                await this.PostMessageAsyncInternal(progress, this.workerCts.Token, postParams, uploadService, uploadItems);
            }
            catch (WebApiException ex)
            {
                this._myStatusError = true;
                this.StatusLabel.Text = $"Err:{ex.Message}(PostMessage)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task PostMessageAsyncInternal(IProgress<string> p, CancellationToken ct, PostStatusParams postParams,
            IMediaUploadService? uploadService, IMediaItem[]? uploadItems)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!CheckAccountValid())
                throw new WebApiException("Auth error. Check your account");

            p.Report("Posting...");

            PostClass? post = null;
            var errMsg = "";

            try
            {
                await Task.Run(async () =>
                {
                    var postParamsWithMedia = postParams;

                    if (uploadService != null && uploadItems != null && uploadItems.Length > 0)
                    {
                        postParamsWithMedia = await uploadService.UploadAsync(uploadItems, postParamsWithMedia)
                            .ConfigureAwait(false);
                    }

                    post = await this.tw.PostStatus(postParamsWithMedia)
                        .ConfigureAwait(false);
                });

                p.Report(Properties.Resources.PostWorker_RunWorkerCompletedText4);
            }
            catch (WebApiException ex)
            {
                // 処理は中断せずエラーの表示のみ行う
                errMsg = $"Err:{ex.Message}(PostMessage)";
                p.Report(errMsg);
                this._myStatusError = true;
            }
            catch (UnauthorizedAccessException ex)
            {
                // アップロード対象のファイルが開けなかった場合など
                errMsg = $"Err:{ex.Message}(PostMessage)";
                p.Report(errMsg);
                this._myStatusError = true;
            }
            finally
            {
                // 使い終わった MediaItem は破棄する
                if (uploadItems != null)
                {
                    foreach (var disposableItem in uploadItems.OfType<IDisposable>())
                    {
                        disposableItem.Dispose();
                    }
                }
            }

            if (ct.IsCancellationRequested)
                return;

            if (!MyCommon.IsNullOrEmpty(errMsg) &&
                !errMsg.StartsWith("OK:", StringComparison.Ordinal) &&
                !errMsg.StartsWith("Warn:", StringComparison.Ordinal))
            {
                var message = string.Format(Properties.Resources.StatusUpdateFailed, errMsg, postParams.Text);

                var ret = MessageBox.Show(
                    message,
                    "Failed to update status",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Question);

                if (ret == DialogResult.Retry)
                {
                    await this.PostMessageAsync(postParams, uploadService, uploadItems);
                }
                else
                {
                    this.StatusTextHistoryBack();
                    this.StatusText.Focus();

                    // 連投モードのときだけEnterイベントが起きないので強制的に背景色を戻す
                    if (SettingManager.Common.FocusLockToStatusText)
                        this.StatusText_Enter(this.StatusText, EventArgs.Empty);
                }
                return;
            }

            this._postTimestamps.Add(DateTimeUtc.Now);

            var oneHour = DateTimeUtc.Now - TimeSpan.FromHours(1);
            foreach (var i in MyCommon.CountDown(this._postTimestamps.Count - 1, 0))
            {
                if (this._postTimestamps[i] < oneHour)
                    this._postTimestamps.RemoveAt(i);
            }

            if (!this.HashMgr.IsPermanent && !MyCommon.IsNullOrEmpty(this.HashMgr.UseHash))
            {
                this.HashMgr.ClearHashtag();
                this.HashStripSplitButton.Text = "#[-]";
                this.HashTogglePullDownMenuItem.Checked = false;
                this.HashToggleMenuItem.Checked = false;
            }

            this.SetMainWindowTitle();

            // TLに反映
            if (!this.tw.UserStreamActive)
            {
                if (SettingManager.Common.PostAndGet)
                    await this.RefreshTabAsync<HomeTabModel>();
                else
                {
                    if (post != null)
                    {
                        this._statuses.AddPost(post);
                        this._statuses.DistributePosts();
                    }
                    this.RefreshTimeline();
                }
            }
        }

        private async Task RetweetAsync(IReadOnlyList<long> statusIds)
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                var progress = new Progress<string>(x => this.StatusLabel.Text = x);

                this.RefreshTasktrayIcon();
                await this.RetweetAsyncInternal(progress, this.workerCts.Token, statusIds);
            }
            catch (WebApiException ex)
            {
                this._myStatusError = true;
                this.StatusLabel.Text = $"Err:{ex.Message}(PostRetweet)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task RetweetAsyncInternal(IProgress<string> p, CancellationToken ct, IReadOnlyList<long> statusIds)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!CheckAccountValid())
                throw new WebApiException("Auth error. Check your account");

            bool read;
            if (!SettingManager.Common.UnreadManage)
                read = true;
            else
                read = this._initial && SettingManager.Common.Read;

            p.Report("Posting...");

            var posts = new List<PostClass>();

            await Task.Run(async () =>
            {
                foreach (var statusId in statusIds)
                {
                    var post = await this.tw.PostRetweet(statusId, read).ConfigureAwait(false);
                    if (post != null) posts.Add(post);
                }
            });

            if (ct.IsCancellationRequested)
                return;

            p.Report(Properties.Resources.PostWorker_RunWorkerCompletedText4);

            this._postTimestamps.Add(DateTimeUtc.Now);

            var oneHour = DateTimeUtc.Now - TimeSpan.FromHours(1);
            foreach (var i in MyCommon.CountDown(this._postTimestamps.Count - 1, 0))
            {
                if (this._postTimestamps[i] < oneHour)
                    this._postTimestamps.RemoveAt(i);
            }

            // TLに反映
            if (!this.tw.UserStreamActive)
            {
                // 自分のRTはTLの更新では取得できない場合があるので、
                // 投稿時取得の有無に関わらず追加しておく
                posts.ForEach(post => this._statuses.AddPost(post));

                if (SettingManager.Common.PostAndGet)
                    await this.RefreshTabAsync<HomeTabModel>();
                else
                {
                    this._statuses.DistributePosts();
                    this.RefreshTimeline();
                }
            }
        }

        private async Task RefreshFollowerIdsAsync()
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                this.RefreshTasktrayIcon();
                this.StatusLabel.Text = Properties.Resources.UpdateFollowersMenuItem1_ClickText1;

                await this.tw.RefreshFollowerIds();

                this.StatusLabel.Text = Properties.Resources.UpdateFollowersMenuItem1_ClickText3;

                this.RefreshTimeline();
                this.PurgeListViewItemCache();
                this.CurrentListView.Refresh();
            }
            catch (WebApiException ex)
            {
                this.StatusLabel.Text = $"Err:{ex.Message}(RefreshFollowersIds)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task RefreshNoRetweetIdsAsync()
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                this.RefreshTasktrayIcon();
                await this.tw.RefreshNoRetweetIds();

                this.StatusLabel.Text = "NoRetweetIds refreshed";
            }
            catch (WebApiException ex)
            {
                this.StatusLabel.Text = $"Err:{ex.Message}(RefreshNoRetweetIds)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task RefreshBlockIdsAsync()
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                this.RefreshTasktrayIcon();
                this.StatusLabel.Text = Properties.Resources.UpdateBlockUserText1;

                await this.tw.RefreshBlockIds();

                this.StatusLabel.Text = Properties.Resources.UpdateBlockUserText3;
            }
            catch (WebApiException ex)
            {
                this.StatusLabel.Text = $"Err:{ex.Message}(RefreshBlockIds)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task RefreshTwitterConfigurationAsync()
        {
            await this.workerSemaphore.WaitAsync();

            try
            {
                this.RefreshTasktrayIcon();
                await this.tw.RefreshConfiguration();

                if (this.tw.Configuration.PhotoSizeLimit != 0)
                {
                    foreach (var service in this.ImageSelector.GetServices())
                    {
                        service.UpdateTwitterConfiguration(this.tw.Configuration);
                    }
                }

                this.PurgeListViewItemCache();
                this.CurrentListView.Refresh();
            }
            catch (WebApiException ex)
            {
                this.StatusLabel.Text = $"Err:{ex.Message}(RefreshConfiguration)";
            }
            finally
            {
                this.workerSemaphore.Release();
            }
        }

        private async Task RefreshMuteUserIdsAsync()
        {
            this.StatusLabel.Text = Properties.Resources.UpdateMuteUserIds_Start;

            try
            {
                await tw.RefreshMuteUserIdsAsync();
            }
            catch (WebApiException ex)
            {
                this.StatusLabel.Text = string.Format(Properties.Resources.UpdateMuteUserIds_Error, ex.Message);
                return;
            }

            this.StatusLabel.Text = Properties.Resources.UpdateMuteUserIds_Finish;
        }

        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Visible = true;
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = _formWindowState;
                }
                this.Activate();
                this.BringToFront();
            }
        }

        private async void MyList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            switch (SettingManager.Common.ListDoubleClickAction)
            {
                case 0:
                    MakeReplyOrDirectStatus();
                    break;
                case 1:
                    await this.FavoriteChange(true);
                    break;
                case 2:
                    var post = this.CurrentPost;
                    if (post != null)
                        await this.ShowUserStatus(post.ScreenName, false);
                    break;
                case 3:
                    await ShowUserTimeline();
                    break;
                case 4:
                    ShowRelatedStatusesMenuItem_Click(this.ShowRelatedStatusesMenuItem, EventArgs.Empty);
                    break;
                case 5:
                    MoveToHomeToolStripMenuItem_Click(this.MoveToHomeToolStripMenuItem, EventArgs.Empty);
                    break;
                case 6:
                    StatusOpenMenuItem_Click(this.StatusOpenMenuItem, EventArgs.Empty);
                    break;
                case 7:
                    //動作なし
                    break;
            }
        }

        private async void FavAddToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.FavoriteChange(true);

        private async void FavRemoveToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.FavoriteChange(false);


        private async void FavoriteRetweetMenuItem_Click(object sender, EventArgs e)
            => await this.FavoritesRetweetOfficial();

        private async void FavoriteRetweetUnofficialMenuItem_Click(object sender, EventArgs e)
            => await this.FavoritesRetweetUnofficial();

        private async Task FavoriteChange(bool FavAdd, bool multiFavoriteChangeDialogEnable = true)
        {
            var tab = this.CurrentTab;
            var posts = tab.SelectedPosts;

            //trueでFavAdd,falseでFavRemove
            if (tab.TabType == MyCommon.TabUsageType.DirectMessage || posts.Length == 0
                || !this.ExistCurrentPost) return;

            if (posts.Length > 1)
            {
                if (FavAdd)
                {
                    // 複数ツイートの一括ふぁぼは禁止
                    // https://support.twitter.com/articles/76915#favoriting
                    MessageBox.Show(string.Format(Properties.Resources.FavoriteLimitCountText, 1));
                    _DoFavRetweetFlags = false;
                    return;
                }
                else
                {
                    if (multiFavoriteChangeDialogEnable)
                    {
                        var confirm = MessageBox.Show(Properties.Resources.FavRemoveToolStripMenuItem_ClickText1,
                            Properties.Resources.FavRemoveToolStripMenuItem_ClickText2,
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                        if (confirm == DialogResult.Cancel)
                            return;
                    }
                }
            }

            if (FavAdd)
            {
                var selectedPost = posts.Single();
                if (selectedPost.IsFav)
                {
                    this.StatusLabel.Text = Properties.Resources.FavAddToolStripMenuItem_ClickText4;
                    return;
                }

                await this.FavAddAsync(selectedPost.StatusId, tab);
            }
            else
            {
                var selectedPosts = posts.Where(x => x.IsFav);
                var statusIds = selectedPosts.Select(x => x.StatusId).ToArray();
                if (statusIds.Length == 0)
                {
                    this.StatusLabel.Text = Properties.Resources.FavRemoveToolStripMenuItem_ClickText4;
                    return;
                }

                await this.FavRemoveAsync(statusIds, tab);
            }
        }

        private PostClass GetCurTabPost(int Index)
        {
            var listCache = this._listItemCache;
            if (listCache != null)
            {
                if (listCache.TryGetValue(Index, out _, out var post))
                    return post;
            }

            return this.CurrentTab[Index];
        }

        private async void MoveToHomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (post != null)
                await this.OpenUriInBrowserAsync(MyCommon.TwitterUrl + post.ScreenName);
            else
                await this.OpenUriInBrowserAsync(MyCommon.TwitterUrl);
        }

        private async void MoveToFavToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (post != null)
                await this.OpenUriInBrowserAsync(MyCommon.TwitterUrl + "#!/" + post.ScreenName + "/favorites");
        }

        private void TweenMain_ClientSizeChanged(object sender, EventArgs e)
        {
            if ((!_initialLayout) && this.Visible)
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    _mySize = this.ClientSize;
                    _mySpDis = this.SplitContainer1.SplitterDistance;
                    _mySpDis3 = this.SplitContainer3.SplitterDistance;
                    if (StatusText.Multiline) _mySpDis2 = this.StatusText.Height;
                    this.MarkSettingLocalModified();
                }
            }
        }

        private void MyList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var comparerMode = this.GetComparerModeByColumnIndex(e.Column);
            if (comparerMode == null)
                return;

            this.SetSortColumn(comparerMode.Value);
        }

        /// <summary>
        /// 列インデックスからソートを行う ComparerMode を求める
        /// </summary>
        /// <param name="columnIndex">ソートを行うカラムのインデックス (表示上の順序とは異なる)</param>
        /// <returns>ソートを行う ComparerMode。null であればソートを行わない</returns>
        private ComparerMode? GetComparerModeByColumnIndex(int columnIndex)
        {
            if (this._iconCol)
                return ComparerMode.Id;

            return columnIndex switch
            {
                1 => ComparerMode.Nickname, // ニックネーム
                2 => ComparerMode.Data, // 本文
                3 => ComparerMode.Id, // 時刻=発言Id
                4 => ComparerMode.Name, // 名前
                7 => ComparerMode.Source, // Source
                _ => (ComparerMode?)null, // 0:アイコン, 5:未読マーク, 6:プロテクト・フィルターマーク
            };
        }

        /// <summary>
        /// 発言一覧の指定した位置の列でソートする
        /// </summary>
        /// <param name="columnIndex">ソートする列の位置 (表示上の順序で指定)</param>
        private void SetSortColumnByDisplayIndex(int columnIndex)
        {
            // 表示上の列の位置から ColumnHeader を求める
            var col = this.CurrentListView.Columns.Cast<ColumnHeader>()
                .FirstOrDefault(x => x.DisplayIndex == columnIndex);

            if (col == null)
                return;

            var comparerMode = this.GetComparerModeByColumnIndex(col.Index);
            if (comparerMode == null)
                return;

            this.SetSortColumn(comparerMode.Value);
        }

        /// <summary>
        /// 発言一覧の最後列の項目でソートする
        /// </summary>
        private void SetSortLastColumn()
        {
            // 表示上の最後列にある ColumnHeader を求める
            var col = this.CurrentListView.Columns.Cast<ColumnHeader>()
                .OrderByDescending(x => x.DisplayIndex)
                .First();

            var comparerMode = this.GetComparerModeByColumnIndex(col.Index);
            if (comparerMode == null)
                return;

            this.SetSortColumn(comparerMode.Value);
        }

        /// <summary>
        /// 発言一覧を指定された ComparerMode に基づいてソートする
        /// </summary>
        private void SetSortColumn(ComparerMode sortColumn)
        {
            if (SettingManager.Common.SortOrderLock)
                return;

            this._statuses.ToggleSortOrder(sortColumn);
            this.InitColumnText();

            var list = this.CurrentListView;
            if (_iconCol)
            {
                list.Columns[0].Text = this.ColumnText[0];
                list.Columns[1].Text = this.ColumnText[2];
            }
            else
            {
                for (var i = 0; i <= 7; i++)
                {
                    list.Columns[i].Text = this.ColumnText[i];
                }
            }

            this.PurgeListViewItemCache();

            var tab = this.CurrentTab;
            var post = this.CurrentPost;
            if (tab.AllCount > 0 && post != null)
            {
                var idx = tab.IndexOf(post.StatusId);
                if (idx > -1)
                {
                    this.SelectListItem(list, idx);
                    list.EnsureVisible(idx);
                }
            }
            list.Refresh();

            this.MarkSettingCommonModified();
        }

        private void TweenMain_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal && !_initialLayout)
            {
                _myLoc = this.DesktopLocation;
                this.MarkSettingLocalModified();
            }
        }

        private void ContextMenuOperate_Opening(object sender, CancelEventArgs e)
        {
            if (!this.ExistCurrentPost)
            {
                ReplyStripMenuItem.Enabled = false;
                ReplyAllStripMenuItem.Enabled = false;
                DMStripMenuItem.Enabled = false;
                ShowProfileMenuItem.Enabled = false;
                ShowUserTimelineContextMenuItem.Enabled = false;
                ListManageUserContextToolStripMenuItem2.Enabled = false;
                MoveToFavToolStripMenuItem.Enabled = false;
                TabMenuItem.Enabled = false;
                IDRuleMenuItem.Enabled = false;
                SourceRuleMenuItem.Enabled = false;
                ReadedStripMenuItem.Enabled = false;
                UnreadStripMenuItem.Enabled = false;
            }
            else
            {
                ShowProfileMenuItem.Enabled = true;
                ListManageUserContextToolStripMenuItem2.Enabled = true;
                ReplyStripMenuItem.Enabled = true;
                ReplyAllStripMenuItem.Enabled = true;
                DMStripMenuItem.Enabled = true;
                ShowUserTimelineContextMenuItem.Enabled = true;
                MoveToFavToolStripMenuItem.Enabled = true;
                TabMenuItem.Enabled = true;
                IDRuleMenuItem.Enabled = true;
                SourceRuleMenuItem.Enabled = true;
                ReadedStripMenuItem.Enabled = true;
                UnreadStripMenuItem.Enabled = true;
            }
            var tab = this.CurrentTab;
            var post = this.CurrentPost;
            if (tab.TabType == MyCommon.TabUsageType.DirectMessage || !this.ExistCurrentPost || post == null || post.IsDm)
            {
                FavAddToolStripMenuItem.Enabled = false;
                FavRemoveToolStripMenuItem.Enabled = false;
                StatusOpenMenuItem.Enabled = false;
                ShowRelatedStatusesMenuItem.Enabled = false;

                ReTweetStripMenuItem.Enabled = false;
                ReTweetUnofficialStripMenuItem.Enabled = false;
                QuoteStripMenuItem.Enabled = false;
                FavoriteRetweetContextMenu.Enabled = false;
                FavoriteRetweetUnofficialContextMenu.Enabled = false;
            }
            else
            {
                FavAddToolStripMenuItem.Enabled = true;
                FavRemoveToolStripMenuItem.Enabled = true;
                StatusOpenMenuItem.Enabled = true;
                ShowRelatedStatusesMenuItem.Enabled = true;  //PublicSearchの時問題出るかも

                if (!post.CanRetweetBy(this.twitterApi.CurrentUserId))
                {
                    ReTweetStripMenuItem.Enabled = false;
                    ReTweetUnofficialStripMenuItem.Enabled = false;
                    QuoteStripMenuItem.Enabled = false;
                    FavoriteRetweetContextMenu.Enabled = false;
                    FavoriteRetweetUnofficialContextMenu.Enabled = false;
                }
                else
                {
                    ReTweetStripMenuItem.Enabled = true;
                    ReTweetUnofficialStripMenuItem.Enabled = true;
                    QuoteStripMenuItem.Enabled = true;
                    FavoriteRetweetContextMenu.Enabled = true;
                    FavoriteRetweetUnofficialContextMenu.Enabled = true;
                }
            }

            if (!this.ExistCurrentPost || post == null || post.InReplyToStatusId == null)
            {
                RepliedStatusOpenMenuItem.Enabled = false;
            }
            else
            {
                RepliedStatusOpenMenuItem.Enabled = true;
            }
            if (!this.ExistCurrentPost || post == null || MyCommon.IsNullOrEmpty(post.RetweetedBy))
            {
                MoveToRTHomeMenuItem.Enabled = false;
            }
            else
            {
                MoveToRTHomeMenuItem.Enabled = true;
            }

            if (this.ExistCurrentPost && post != null)
            {
                this.DeleteStripMenuItem.Enabled = post.CanDeleteBy(this.tw.UserId);
                if (post.RetweetedByUserId == this.tw.UserId)
                    this.DeleteStripMenuItem.Text = Properties.Resources.DeleteMenuText2;
                else
                    this.DeleteStripMenuItem.Text = Properties.Resources.DeleteMenuText1;
            }
        }

        private void ReplyStripMenuItem_Click(object sender, EventArgs e)
            => this.MakeReplyOrDirectStatus(false, true);

        private void DMStripMenuItem_Click(object sender, EventArgs e)
            => this.MakeReplyOrDirectStatus(false, false);

        private async Task doStatusDelete()
        {
            var posts = this.CurrentTab.SelectedPosts;
            if (posts.Length == 0)
                return;

            // 選択されたツイートの中に削除可能なものが一つでもあるか
            if (!posts.Any(x => x.CanDeleteBy(this.tw.UserId)))
                return;

            var ret = MessageBox.Show(this,
                string.Format(Properties.Resources.DeleteStripMenuItem_ClickText1, Environment.NewLine),
                Properties.Resources.DeleteStripMenuItem_ClickText2,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (ret != DialogResult.OK)
                return;

            var currentListView = this.CurrentListView;
            var focusedIndex = currentListView.FocusedItem?.Index ?? currentListView.TopItem?.Index ?? 0;

            using (ControlTransaction.Cursor(this, Cursors.WaitCursor))
            {
                Exception? lastException = null;
                foreach (var post in posts)
                {
                    if (!post.CanDeleteBy(this.tw.UserId))
                        continue;

                    try
                    {
                        if (post.IsDm)
                        {
                            await this.twitterApi.DirectMessagesEventsDestroy(post.StatusId.ToString(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            if (post.RetweetedByUserId == this.tw.UserId)
                            {
                                // 自分が RT したツイート (自分が RT した自分のツイートも含む)
                                //   => RT を取り消し
                                await this.twitterApi.StatusesDestroy(post.StatusId)
                                    .IgnoreResponse();
                            }
                            else
                            {
                                if (post.UserId == this.tw.UserId)
                                {
                                    if (post.RetweetedId != null)
                                        // 他人に RT された自分のツイート
                                        //   => RT 元の自分のツイートを削除
                                        await this.twitterApi.StatusesDestroy(post.RetweetedId.Value)
                                            .IgnoreResponse();
                                    else
                                        // 自分のツイート
                                        //   => ツイートを削除
                                        await this.twitterApi.StatusesDestroy(post.StatusId)
                                            .IgnoreResponse();
                                }
                            }
                        }
                    }
                    catch (WebApiException ex)
                    {
                        lastException = ex;
                        continue;
                    }

                    this._statuses.RemovePostFromAllTabs(post.StatusId, setIsDeleted: true);
                }

                if (lastException == null)
                    this.StatusLabel.Text = Properties.Resources.DeleteStripMenuItem_ClickText4; // 成功
                else
                    this.StatusLabel.Text = Properties.Resources.DeleteStripMenuItem_ClickText3; // 失敗

                this.PurgeListViewItemCache();

                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    var tabPage = this.ListTab.TabPages[index];
                    var listView = (DetailsListView)tabPage.Tag;

                    using (ControlTransaction.Update(listView))
                    {
                        listView.VirtualListSize = tab.AllCount;

                        if (tab.TabName == this.CurrentTabName)
                        {
                            listView.SelectedIndices.Clear();

                            if (tab.AllCount != 0)
                            {
                                int selectedIndex;
                                if (tab.AllCount - 1 > focusedIndex && focusedIndex > -1)
                                    selectedIndex = focusedIndex;
                                else
                                    selectedIndex = tab.AllCount - 1;

                                listView.SelectedIndices.Add(selectedIndex);
                                listView.EnsureVisible(selectedIndex);
                                listView.FocusedItem = listView.Items[selectedIndex];
                            }
                        }
                    }

                    if (SettingManager.Common.TabIconDisp && tab.UnreadCount == 0)
                    {
                        if (tabPage.ImageIndex == 0)
                            tabPage.ImageIndex = -1; // タブアイコン
                    }
                }

                if (!SettingManager.Common.TabIconDisp)
                    this.ListTab.Refresh();
            }
        }

        private async void DeleteStripMenuItem_Click(object sender, EventArgs e)
            => await this.doStatusDelete();

        private void ReadedStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ControlTransaction.Update(this.CurrentListView))
            {
                var tab = this.CurrentTab;
                foreach (var statusId in tab.SelectedStatusIds)
                {
                    this._statuses.SetReadAllTab(statusId, read: true);
                    var idx = tab.IndexOf(statusId);
                    ChangeCacheStyleRead(true, idx);
                }
                ColorizeList();
            }
            if (SettingManager.Common.TabIconDisp)
            {
                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    if (tab.UnreadCount == 0)
                    {
                        var tabPage = this.ListTab.TabPages[index];
                        if (tabPage.ImageIndex == 0)
                            tabPage.ImageIndex = -1; // タブアイコン
                    }
                }
            }
            if (!SettingManager.Common.TabIconDisp) ListTab.Refresh();
        }

        private void UnreadStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ControlTransaction.Update(this.CurrentListView))
            {
                var tab = this.CurrentTab;
                foreach (var statusId in tab.SelectedStatusIds)
                {
                    this._statuses.SetReadAllTab(statusId, read: false);
                    var idx = tab.IndexOf(statusId);
                    ChangeCacheStyleRead(false, idx);
                }
                ColorizeList();
            }
            if (SettingManager.Common.TabIconDisp)
            {
                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    if (tab.UnreadCount > 0)
                    {
                        var tabPage = this.ListTab.TabPages[index];
                        if (tabPage.ImageIndex == -1)
                            tabPage.ImageIndex = 0; // タブアイコン
                    }
                }
            }
            if (!SettingManager.Common.TabIconDisp) ListTab.Refresh();
        }

        private async void RefreshStripMenuItem_Click(object sender, EventArgs e)
            => await this.DoRefresh();

        private async Task DoRefresh()
            => await this.RefreshTabAsync(this.CurrentTab);

        private async Task DoRefreshMore()
            => await this.RefreshTabAsync(this.CurrentTab, backward: true);

        private DialogResult ShowSettingDialog(bool showTaskbarIcon = false)
        {
            var result = DialogResult.Abort;

            using var settingDialog = new AppendSettingDialog();
            settingDialog.Icon = this.MainIcon;
            settingDialog.Owner = this;
            settingDialog.ShowInTaskbar = showTaskbarIcon;
            settingDialog.IntervalChanged += this.TimerInterval_Changed;

            settingDialog.tw = this.tw;
            settingDialog.twitterApi = this.twitterApi;

            settingDialog.LoadConfig(SettingManager.Common, SettingManager.Local);

            try
            {
                result = settingDialog.ShowDialog(this);
            }
            catch (Exception)
            {
                return DialogResult.Abort;
            }

            if (result == DialogResult.OK)
            {
                lock (_syncObject)
                {
                    settingDialog.SaveConfig(SettingManager.Common, SettingManager.Local);
                }
            }

            return result;
        }

        private async void SettingStripMenuItem_Click(object sender, EventArgs e)
        {
            // 設定画面表示前のユーザー情報
            var oldUser = new { tw.AccessToken, tw.AccessTokenSecret, tw.Username, tw.UserId };

            var oldIconSz = SettingManager.Common.IconSize;

            if (ShowSettingDialog() == DialogResult.OK)
            {
                lock (_syncObject)
                {
                    tw.RestrictFavCheck = SettingManager.Common.RestrictFavCheck;
                    tw.ReadOwnPost = SettingManager.Common.ReadOwnPost;
                    ShortUrl.Instance.DisableExpanding = !SettingManager.Common.TinyUrlResolve;
                    ShortUrl.Instance.BitlyAccessToken = SettingManager.Common.BitlyAccessToken;
                    ShortUrl.Instance.BitlyId = SettingManager.Common.BilyUser;
                    ShortUrl.Instance.BitlyKey = SettingManager.Common.BitlyPwd;
                    TwitterApiConnection.RestApiHost = SettingManager.Common.TwitterApiHost;

                    Networking.DefaultTimeout = TimeSpan.FromSeconds(SettingManager.Common.DefaultTimeOut);
                    Networking.UploadImageTimeout = TimeSpan.FromSeconds(SettingManager.Common.UploadImageTimeout);
                    Networking.SetWebProxy(SettingManager.Local.ProxyType,
                        SettingManager.Local.ProxyAddress, SettingManager.Local.ProxyPort,
                        SettingManager.Local.ProxyUser, SettingManager.Local.ProxyPassword);
                    Networking.ForceIPv4 = SettingManager.Common.ForceIPv4;

                    ImageSelector.Reset(tw, this.tw.Configuration);

                    try
                    {
                        if (SettingManager.Common.TabIconDisp)
                        {
                            ListTab.DrawItem -= ListTab_DrawItem;
                            ListTab.DrawMode = TabDrawMode.Normal;
                            ListTab.ImageList = this.TabImage;
                        }
                        else
                        {
                            ListTab.DrawItem -= ListTab_DrawItem;
                            ListTab.DrawItem += ListTab_DrawItem;
                            ListTab.DrawMode = TabDrawMode.OwnerDrawFixed;
                            ListTab.ImageList = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Data["Instance"] = "ListTab(TabIconDisp)";
                        ex.Data["IsTerminatePermission"] = false;
                        throw;
                    }

                    try
                    {
                        if (!SettingManager.Common.UnreadManage)
                        {
                            ReadedStripMenuItem.Enabled = false;
                            UnreadStripMenuItem.Enabled = false;
                            if (SettingManager.Common.TabIconDisp)
                            {
                                foreach (TabPage myTab in ListTab.TabPages)
                                {
                                    myTab.ImageIndex = -1;
                                }
                            }
                        }
                        else
                        {
                            ReadedStripMenuItem.Enabled = true;
                            UnreadStripMenuItem.Enabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Data["Instance"] = "ListTab(UnreadManage)";
                        ex.Data["IsTerminatePermission"] = false;
                        throw;
                    }

                    // タブの表示位置の決定
                    SetTabAlignment();

                    SplitContainer1.IsPanelInverted = !SettingManager.Common.StatusAreaAtBottom;

                    var imgazyobizinet = ThumbnailGenerator.ImgAzyobuziNetInstance;
                    imgazyobizinet.Enabled = SettingManager.Common.EnableImgAzyobuziNet;
                    imgazyobizinet.DisabledInDM = SettingManager.Common.ImgAzyobuziNetDisabledInDM;

                    this.PlaySoundMenuItem.Checked = SettingManager.Common.PlaySound;
                    this.PlaySoundFileMenuItem.Checked = SettingManager.Common.PlaySound;
                    _fntUnread = SettingManager.Local.FontUnread;
                    _clUnread = SettingManager.Local.ColorUnread;
                    _fntReaded = SettingManager.Local.FontRead;
                    _clReaded = SettingManager.Local.ColorRead;
                    _clFav = SettingManager.Local.ColorFav;
                    _clOWL = SettingManager.Local.ColorOWL;
                    _clRetweet = SettingManager.Local.ColorRetweet;
                    _fntDetail = SettingManager.Local.FontDetail;
                    _clDetail = SettingManager.Local.ColorDetail;
                    _clDetailLink = SettingManager.Local.ColorDetailLink;
                    _clDetailBackcolor = SettingManager.Local.ColorDetailBackcolor;
                    _clSelf = SettingManager.Local.ColorSelf;
                    _clAtSelf = SettingManager.Local.ColorAtSelf;
                    _clTarget = SettingManager.Local.ColorTarget;
                    _clAtTarget = SettingManager.Local.ColorAtTarget;
                    _clAtFromTarget = SettingManager.Local.ColorAtFromTarget;
                    _clAtTo = SettingManager.Local.ColorAtTo;
                    _clListBackcolor = SettingManager.Local.ColorListBackcolor;
                    _clInputBackcolor = SettingManager.Local.ColorInputBackcolor;
                    _clInputFont = SettingManager.Local.ColorInputFont;
                    _fntInputFont = SettingManager.Local.FontInputFont;
                    _brsBackColorMine.Dispose();
                    _brsBackColorAt.Dispose();
                    _brsBackColorYou.Dispose();
                    _brsBackColorAtYou.Dispose();
                    _brsBackColorAtFromTarget.Dispose();
                    _brsBackColorAtTo.Dispose();
                    _brsBackColorNone.Dispose();
                    _brsBackColorMine = new SolidBrush(_clSelf);
                    _brsBackColorAt = new SolidBrush(_clAtSelf);
                    _brsBackColorYou = new SolidBrush(_clTarget);
                    _brsBackColorAtYou = new SolidBrush(_clAtTarget);
                    _brsBackColorAtFromTarget = new SolidBrush(_clAtFromTarget);
                    _brsBackColorAtTo = new SolidBrush(_clAtTo);
                    _brsBackColorNone = new SolidBrush(_clListBackcolor);

                    try
                    {
                        if (StatusText.Focused) StatusText.BackColor = _clInputBackcolor;
                        StatusText.Font = _fntInputFont;
                        StatusText.ForeColor = _clInputFont;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }

                    try
                    {
                        InitDetailHtmlFormat();
                    }
                    catch (Exception ex)
                    {
                        ex.Data["Instance"] = "Font";
                        ex.Data["IsTerminatePermission"] = false;
                        throw;
                    }

                    try
                    {
                        if (SettingManager.Common.TabIconDisp)
                        {
                            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                            {
                                var tabPage = this.ListTab.TabPages[index];
                                if (tab.UnreadCount == 0)
                                    tabPage.ImageIndex = -1;
                                else
                                    tabPage.ImageIndex = 0;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Data["Instance"] = "ListTab(TabIconDisp no2)";
                        ex.Data["IsTerminatePermission"] = false;
                        throw;
                    }

                    try
                    {
                        var oldIconCol = _iconCol;

                        if (SettingManager.Common.IconSize != oldIconSz)
                            ApplyListViewIconSize(SettingManager.Common.IconSize);

                        foreach (TabPage tp in ListTab.TabPages)
                        {
                            var lst = (DetailsListView)tp.Tag;

                            using (ControlTransaction.Update(lst))
                            {
                                lst.GridLines = SettingManager.Common.ShowGrid;
                                lst.Font = _fntReaded;
                                lst.BackColor = _clListBackcolor;

                                if (_iconCol != oldIconCol)
                                    ResetColumns(lst);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Data["Instance"] = "ListView(IconSize)";
                        ex.Data["IsTerminatePermission"] = false;
                        throw;
                    }

                    SetMainWindowTitle();
                    SetNotifyIconText();

                    this.PurgeListViewItemCache();
                    this.CurrentListView.Refresh();
                    ListTab.Refresh();

                    _hookGlobalHotkey.UnregisterAllOriginalHotkey();
                    if (SettingManager.Common.HotkeyEnabled)
                    {
                        ///グローバルホットキーの登録。設定で変更可能にするかも
                        var modKey = HookGlobalHotkey.ModKeys.None;
                        if ((SettingManager.Common.HotkeyModifier & Keys.Alt) == Keys.Alt)
                            modKey |= HookGlobalHotkey.ModKeys.Alt;
                        if ((SettingManager.Common.HotkeyModifier & Keys.Control) == Keys.Control)
                            modKey |= HookGlobalHotkey.ModKeys.Ctrl;
                        if ((SettingManager.Common.HotkeyModifier & Keys.Shift) == Keys.Shift)
                            modKey |=  HookGlobalHotkey.ModKeys.Shift;
                        if ((SettingManager.Common.HotkeyModifier & Keys.LWin) == Keys.LWin)
                            modKey |= HookGlobalHotkey.ModKeys.Win;

                        _hookGlobalHotkey.RegisterOriginalHotkey(SettingManager.Common.HotkeyKey, SettingManager.Common.HotkeyValue, modKey);
                    }

                    if (SettingManager.Common.IsUseNotifyGrowl) gh.RegisterGrowl();
                    try
                    {
                        StatusText_TextChanged(this.StatusText, EventArgs.Empty);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            else
            {
                // キャンセル時は Twitter クラスの認証情報を画面表示前の状態に戻す
                this.tw.Initialize(oldUser.AccessToken, oldUser.AccessTokenSecret, oldUser.Username, oldUser.UserId);
            }

            Twitter.AccountState = MyCommon.ACCOUNT_STATE.Valid;

            this.TopMost = SettingManager.Common.AlwaysTop;
            SaveConfigsAll(false);

            if (tw.UserId != oldUser.UserId)
                await this.doGetFollowersMenu();
        }

        /// <summary>
        /// タブの表示位置を設定する
        /// </summary>
        private void SetTabAlignment()
        {
            var newAlignment = SettingManager.Common.ViewTabBottom ? TabAlignment.Bottom : TabAlignment.Top;
            if (ListTab.Alignment == newAlignment) return;

            // 各タブのリスト上の選択位置などを退避
            var listSelections = this.SaveListViewSelection();

            ListTab.Alignment = newAlignment;

            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var lst = (DetailsListView)this.ListTab.TabPages[index].Tag;
                using (ControlTransaction.Update(lst))
                {
                    // 選択位置などを復元
                    this.RestoreListViewSelection(lst, tab, listSelections[tab.TabName]);
                }
            }
        }

        private void ApplyListViewIconSize(MyCommon.IconSizes iconSz)
        {
            // アイコンサイズの再設定
            this._iconSz = iconSz switch
            {
                MyCommon.IconSizes.IconNone => 0,
                MyCommon.IconSizes.Icon16 => 16,
                MyCommon.IconSizes.Icon24 => 26,
                MyCommon.IconSizes.Icon48 => 48,
                MyCommon.IconSizes.Icon48_2 => 48,
                _ => throw new InvalidEnumArgumentException(nameof(iconSz), (int)iconSz, typeof(MyCommon.IconSizes)),
            };
            this._iconCol = iconSz == MyCommon.IconSizes.Icon48_2;

            if (_iconSz > 0)
            {
                // ディスプレイの DPI 設定を考慮したサイズを設定する
                _listViewImageList.ImageSize = new Size(
                    1,
                    (int)Math.Ceiling(this._iconSz * this.CurrentScaleFactor.Height));
            }
            else
            {
                _listViewImageList.ImageSize = new Size(1, 1);
            }
        }

        private void ResetColumns(DetailsListView list)
        {
            using (ControlTransaction.Update(list))
            using (ControlTransaction.Layout(list, false))
            {
                // カラムヘッダの再設定
                list.ColumnClick -= MyList_ColumnClick;
                list.DrawColumnHeader -= MyList_DrawColumnHeader;
                list.ColumnReordered -= MyList_ColumnReordered;
                list.ColumnWidthChanged -= MyList_ColumnWidthChanged;

                var cols = list.Columns.Cast<ColumnHeader>().ToList();
                list.Columns.Clear();
                cols.ForEach(col => col.Dispose());
                cols.Clear();

                InitColumns(list, true);

                list.ColumnClick += MyList_ColumnClick;
                list.DrawColumnHeader += MyList_DrawColumnHeader;
                list.ColumnReordered += MyList_ColumnReordered;
                list.ColumnWidthChanged += MyList_ColumnWidthChanged;
            }
        }

        public void AddNewTabForSearch(string searchWord)
        {
            //同一検索条件のタブが既に存在すれば、そのタブアクティブにして終了
            foreach (var tb in _statuses.GetTabsByType<PublicSearchTabModel>())
            {
                if (tb.SearchWords == searchWord && MyCommon.IsNullOrEmpty(tb.SearchLang))
                {
                    var tabIndex = this._statuses.Tabs.IndexOf(tb);
                    this.ListTab.SelectedIndex = tabIndex;
                    return;
                }
            }
            //ユニークなタブ名生成
            var tabName = searchWord;
            for (var i = 0; i <= 100; i++)
            {
                if (_statuses.ContainsTab(tabName))
                    tabName += "_";
                else
                    break;
            }
            //タブ追加
            var tab = new PublicSearchTabModel(tabName);
            _statuses.AddTab(tab);
            AddNewTab(tab, startup: false);
            //追加したタブをアクティブに
            ListTab.SelectedIndex = this._statuses.Tabs.Count - 1;
            //検索条件の設定
            var tabPage = this.CurrentTabPage;
            var cmb = (ComboBox)tabPage.Controls["panelSearch"].Controls["comboSearch"];
            cmb.Items.Add(searchWord);
            cmb.Text = searchWord;
            SaveConfigsTabs();
            //検索実行
            this.SearchButton_Click(tabPage.Controls["panelSearch"].Controls["comboSearch"], EventArgs.Empty);
        }

        private async Task ShowUserTimeline()
        {
            var post = this.CurrentPost;
            if (post == null || !this.ExistCurrentPost) return;
            await this.AddNewTabForUserTimeline(post.ScreenName);
        }

        private void SearchComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                RemoveSpecifiedTab(this.CurrentTabName, false);
                SaveConfigsTabs();
                e.SuppressKeyPress = true;
            }
        }

        public async Task AddNewTabForUserTimeline(string user)
        {
            //同一検索条件のタブが既に存在すれば、そのタブアクティブにして終了
            foreach (var tb in _statuses.GetTabsByType<UserTimelineTabModel>())
            {
                if (tb.ScreenName == user)
                {
                    var tabIndex = this._statuses.Tabs.IndexOf(tb);
                    this.ListTab.SelectedIndex = tabIndex;
                    return;
                }
            }
            //ユニークなタブ名生成
            var tabName = "user:" + user;
            while (_statuses.ContainsTab(tabName))
            {
                tabName += "_";
            }
            //タブ追加
            var tab = new UserTimelineTabModel(tabName, user);
            this._statuses.AddTab(tab);
            this.AddNewTab(tab, startup: false);
            //追加したタブをアクティブに
            ListTab.SelectedIndex = this._statuses.Tabs.Count - 1;
            SaveConfigsTabs();
            //検索実行
            await this.RefreshTabAsync(tab);
        }

        public bool AddNewTab(TabModel tab, bool startup)
        {
            //重複チェック
            if (this.ListTab.TabPages.Cast<TabPage>().Any(x => x.Text == tab.TabName))
                return false;

            //新規タブ名チェック
            if (tab.TabName == Properties.Resources.AddNewTabText1) return false;

            var _tabPage = new TabPage();
            var _listCustom = new DetailsListView();

            var cnt = this._statuses.Tabs.Count;

            ///ToDo:Create and set controls follow tabtypes

            using (ControlTransaction.Update(_listCustom))
            using (ControlTransaction.Layout(this.SplitContainer1.Panel1, false))
            using (ControlTransaction.Layout(this.SplitContainer1.Panel2, false))
            using (ControlTransaction.Layout(this.SplitContainer1, false))
            using (ControlTransaction.Layout(this.ListTab, false))
            using (ControlTransaction.Layout(this))
            using (ControlTransaction.Layout(_tabPage, false))
            {
                _tabPage.Controls.Add(_listCustom);

                /// UserTimeline関連
                var userTab = tab as UserTimelineTabModel;
                var listTab = tab as ListTimelineTabModel;
                var searchTab = tab as PublicSearchTabModel;

                if (userTab != null || listTab != null)
                {
                    var label = new Label
                    {
                        Dock = DockStyle.Top,
                        Name = "labelUser",
                        TabIndex = 0,
                    };

                    if (listTab != null)
                    {
                        label.Text = listTab.ListInfo.ToString();
                    }
                    else if (userTab != null)
                    {
                        label.Text = userTab.ScreenName + "'s Timeline";
                    }
                    label.TextAlign = ContentAlignment.MiddleLeft;
                    using (var tmpComboBox = new ComboBox())
                    {
                        label.Height = tmpComboBox.Height;
                    }
                    _tabPage.Controls.Add(label);
                }
                /// 検索関連の準備
                else if (searchTab != null)
                {
                    var pnl = new Panel();

                    var lbl = new Label();
                    var cmb = new ComboBox();
                    var btn = new Button();
                    var cmbLang = new ComboBox();

                    using (ControlTransaction.Layout(pnl, false))
                    {
                        pnl.Controls.Add(cmb);
                        pnl.Controls.Add(cmbLang);
                        pnl.Controls.Add(btn);
                        pnl.Controls.Add(lbl);
                        pnl.Name = "panelSearch";
                        pnl.TabIndex = 0;
                        pnl.Dock = DockStyle.Top;
                        pnl.Height = cmb.Height;
                        pnl.Enter += SearchControls_Enter;
                        pnl.Leave += SearchControls_Leave;

                        cmb.Text = "";
                        cmb.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                        cmb.Dock = DockStyle.Fill;
                        cmb.Name = "comboSearch";
                        cmb.DropDownStyle = ComboBoxStyle.DropDown;
                        cmb.ImeMode = ImeMode.NoControl;
                        cmb.TabStop = false;
                        cmb.TabIndex = 1;
                        cmb.AutoCompleteMode = AutoCompleteMode.None;
                        cmb.KeyDown += SearchComboBox_KeyDown;

                        cmbLang.Text = "";
                        cmbLang.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                        cmbLang.Dock = DockStyle.Right;
                        cmbLang.Width = 50;
                        cmbLang.Name = "comboLang";
                        cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
                        cmbLang.TabStop = false;
                        cmbLang.TabIndex = 2;
                        cmbLang.Items.Add("");
                        cmbLang.Items.Add("ja");
                        cmbLang.Items.Add("en");
                        cmbLang.Items.Add("ar");
                        cmbLang.Items.Add("da");
                        cmbLang.Items.Add("nl");
                        cmbLang.Items.Add("fa");
                        cmbLang.Items.Add("fi");
                        cmbLang.Items.Add("fr");
                        cmbLang.Items.Add("de");
                        cmbLang.Items.Add("hu");
                        cmbLang.Items.Add("is");
                        cmbLang.Items.Add("it");
                        cmbLang.Items.Add("no");
                        cmbLang.Items.Add("pl");
                        cmbLang.Items.Add("pt");
                        cmbLang.Items.Add("ru");
                        cmbLang.Items.Add("es");
                        cmbLang.Items.Add("sv");
                        cmbLang.Items.Add("th");

                        lbl.Text = "Search(C-S-f)";
                        lbl.Name = "label1";
                        lbl.Dock = DockStyle.Left;
                        lbl.Width = 90;
                        lbl.Height = cmb.Height;
                        lbl.TextAlign = ContentAlignment.MiddleLeft;
                        lbl.TabIndex = 0;

                        btn.Text = "Search";
                        btn.Name = "buttonSearch";
                        btn.UseVisualStyleBackColor = true;
                        btn.Dock = DockStyle.Right;
                        btn.TabStop = false;
                        btn.TabIndex = 3;
                        btn.Click += SearchButton_Click;

                        if (!MyCommon.IsNullOrEmpty(searchTab.SearchWords))
                        {
                            cmb.Items.Add(searchTab.SearchWords);
                            cmb.Text = searchTab.SearchWords;
                        }

                        cmbLang.Text = searchTab.SearchLang;

                        _tabPage.Controls.Add(pnl);
                    }
                }

                _tabPage.Tag = _listCustom;
                this.ListTab.Controls.Add(_tabPage);

                _tabPage.Location = new Point(4, 4);
                _tabPage.Name = "CTab" + cnt;
                _tabPage.Size = new Size(380, 260);
                _tabPage.TabIndex = 2 + cnt;
                _tabPage.Text = tab.TabName;
                _tabPage.UseVisualStyleBackColor = true;
                _tabPage.AccessibleRole = AccessibleRole.PageTab;

                _listCustom.AccessibleName = Properties.Resources.AddNewTab_ListView_AccessibleName;
                _listCustom.TabIndex = 1;
                _listCustom.AllowColumnReorder = true;
                _listCustom.ContextMenuStrip = this.ContextMenuOperate;
                _listCustom.ColumnHeaderContextMenuStrip = this.ContextMenuColumnHeader;
                _listCustom.Dock = DockStyle.Fill;
                _listCustom.FullRowSelect = true;
                _listCustom.HideSelection = false;
                _listCustom.Location = new Point(0, 0);
                _listCustom.Margin = new Padding(0);
                _listCustom.Name = "CList" + Environment.TickCount;
                _listCustom.ShowItemToolTips = true;
                _listCustom.Size = new Size(380, 260);
                _listCustom.UseCompatibleStateImageBehavior = false;
                _listCustom.View = View.Details;
                _listCustom.OwnerDraw = true;
                _listCustom.VirtualMode = true;
                _listCustom.Font = _fntReaded;
                _listCustom.BackColor = _clListBackcolor;

                _listCustom.GridLines = SettingManager.Common.ShowGrid;
                _listCustom.AllowDrop = true;

                _listCustom.SmallImageList = _listViewImageList;

                InitColumns(_listCustom, startup);

                _listCustom.SelectedIndexChanged += MyList_SelectedIndexChanged;
                _listCustom.MouseDoubleClick += MyList_MouseDoubleClick;
                _listCustom.ColumnClick += MyList_ColumnClick;
                _listCustom.DrawColumnHeader += MyList_DrawColumnHeader;
                _listCustom.DragDrop += TweenMain_DragDrop;
                _listCustom.DragEnter += TweenMain_DragEnter;
                _listCustom.DragOver += TweenMain_DragOver;
                _listCustom.DrawItem += MyList_DrawItem;
                _listCustom.MouseClick += MyList_MouseClick;
                _listCustom.ColumnReordered += MyList_ColumnReordered;
                _listCustom.ColumnWidthChanged += MyList_ColumnWidthChanged;
                _listCustom.CacheVirtualItems += MyList_CacheVirtualItems;
                _listCustom.RetrieveVirtualItem += MyList_RetrieveVirtualItem;
                _listCustom.DrawSubItem += MyList_DrawSubItem;
                _listCustom.HScrolled += MyList_HScrolled;
            }

            return true;
        }

        public bool RemoveSpecifiedTab(string TabName, bool confirm)
        {
            var tabInfo = _statuses.GetTabByName(TabName);
            if (tabInfo == null || tabInfo.IsDefaultTabType || tabInfo.Protected)
                return false;

            if (confirm)
            {
                var tmp = string.Format(Properties.Resources.RemoveSpecifiedTabText1, Environment.NewLine);
                if (MessageBox.Show(tmp, TabName + " " + Properties.Resources.RemoveSpecifiedTabText2,
                                 MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Cancel)
                {
                    return false;
                }
            }

            var tabIndex = this._statuses.Tabs.IndexOf(TabName);
            if (tabIndex == -1)
                return false;

            var _tabPage = this.ListTab.TabPages[tabIndex];

            SetListProperty();   //他のタブに列幅等を反映

            //オブジェクトインスタンスの削除
            var _listCustom = (DetailsListView)_tabPage.Tag;
            _tabPage.Tag = null;

            using (ControlTransaction.Layout(this.SplitContainer1.Panel1, false))
            using (ControlTransaction.Layout(this.SplitContainer1.Panel2, false))
            using (ControlTransaction.Layout(this.SplitContainer1, false))
            using (ControlTransaction.Layout(this.ListTab, false))
            using (ControlTransaction.Layout(this))
            using (ControlTransaction.Layout(_tabPage, false))
            {
                if (this.CurrentTabName == TabName)
                {
                    this.ListTab.SelectTab((this._beforeSelectedTab != null && this.ListTab.TabPages.Contains(this._beforeSelectedTab)) ? this._beforeSelectedTab : this.ListTab.TabPages[0]);
                    this._beforeSelectedTab = null;
                }
                this.ListTab.Controls.Remove(_tabPage);

                // 後付けのコントロールを破棄
                if (tabInfo.TabType == MyCommon.TabUsageType.UserTimeline || tabInfo.TabType == MyCommon.TabUsageType.Lists)
                {
                    using var label = _tabPage.Controls["labelUser"];
                    _tabPage.Controls.Remove(label);
                }
                else if (tabInfo.TabType == MyCommon.TabUsageType.PublicSearch)
                {
                    using var pnl = _tabPage.Controls["panelSearch"];

                    pnl.Enter -= SearchControls_Enter;
                    pnl.Leave -= SearchControls_Leave;
                    _tabPage.Controls.Remove(pnl);

                    foreach (Control ctrl in pnl.Controls)
                    {
                        if (ctrl.Name == "buttonSearch")
                        {
                            ctrl.Click -= SearchButton_Click;
                        }
                        else if (ctrl.Name == "comboSearch")
                        {
                            ctrl.KeyDown -= SearchComboBox_KeyDown;
                        }
                        pnl.Controls.Remove(ctrl);
                        ctrl.Dispose();
                    }
                }

                _tabPage.Controls.Remove(_listCustom);

                _listCustom.SelectedIndexChanged -= MyList_SelectedIndexChanged;
                _listCustom.MouseDoubleClick -= MyList_MouseDoubleClick;
                _listCustom.ColumnClick -= MyList_ColumnClick;
                _listCustom.DrawColumnHeader -= MyList_DrawColumnHeader;
                _listCustom.DragDrop -= TweenMain_DragDrop;
                _listCustom.DragEnter -= TweenMain_DragEnter;
                _listCustom.DragOver -= TweenMain_DragOver;
                _listCustom.DrawItem -= MyList_DrawItem;
                _listCustom.MouseClick -= MyList_MouseClick;
                _listCustom.ColumnReordered -= MyList_ColumnReordered;
                _listCustom.ColumnWidthChanged -= MyList_ColumnWidthChanged;
                _listCustom.CacheVirtualItems -= MyList_CacheVirtualItems;
                _listCustom.RetrieveVirtualItem -= MyList_RetrieveVirtualItem;
                _listCustom.DrawSubItem -= MyList_DrawSubItem;
                _listCustom.HScrolled -= MyList_HScrolled;

                var cols = _listCustom.Columns.Cast<ColumnHeader>().ToList<ColumnHeader>();
                _listCustom.Columns.Clear();
                cols.ForEach(col => col.Dispose());
                cols.Clear();

                _listCustom.ContextMenuStrip = null;
                _listCustom.ColumnHeaderContextMenuStrip = null;
                _listCustom.Font = null;

                _listCustom.SmallImageList = null;
                _listCustom.ListViewItemSorter = null;

                // キャッシュのクリア
                this.PurgeListViewItemCache();
            }

            _tabPage.Dispose();
            _listCustom.Dispose();
            _statuses.RemoveTab(TabName);

            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var lst = (DetailsListView)this.ListTab.TabPages[index].Tag;
                lst.VirtualListSize = tab.AllCount;
            }

            return true;
        }

        private void ListTab_Deselected(object sender, TabControlEventArgs e)
        {
            this.PurgeListViewItemCache();
            _beforeSelectedTab = e.TabPage;
        }

        private void ListTab_MouseMove(object sender, MouseEventArgs e)
        {
            //タブのD&D

            if (!SettingManager.Common.TabMouseLock && e.Button == MouseButtons.Left && _tabDrag)
            {
                var tn = "";
                var dragEnableRectangle = new Rectangle(_tabMouseDownPoint.X - (SystemInformation.DragSize.Width / 2), _tabMouseDownPoint.Y - (SystemInformation.DragSize.Height / 2), SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
                if (!dragEnableRectangle.Contains(e.Location))
                {
                    //タブが多段の場合にはMouseDownの前の段階で選択されたタブの段が変わっているので、このタイミングでカーソルの位置からタブを判定出来ない。
                    tn = this.CurrentTabName;
                }

                if (MyCommon.IsNullOrEmpty(tn)) return;

                var tabIndex = this._statuses.Tabs.IndexOf(tn);
                if (tabIndex != -1)
                {
                    var tabPage = this.ListTab.TabPages[tabIndex];
                    ListTab.DoDragDrop(tabPage, DragDropEffects.All);
                }
            }
            else
            {
                _tabDrag = false;
            }

            var cpos = new Point(e.X, e.Y);
            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var rect = ListTab.GetTabRect(index);
                if (rect.Contains(cpos))
                {
                    _rclickTabName = tab.TabName;
                    break;
                }
            }
        }

        private void ListTab_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetMainWindowTitle();
            SetStatusLabelUrl();
            SetApiStatusLabel();
            if (ListTab.Focused || ((Control)this.CurrentTabPage.Tag).Focused)
                this.Tag = ListTab.Tag;
            TabMenuControl(this.CurrentTabName);
            this.PushSelectPostChain();
            DispSelectedPost();
        }

        private void SetListProperty()
        {
            if (!_isColumnChanged) return;

            var currentListView = this.CurrentListView;

            var dispOrder = new int[currentListView.Columns.Count];
            for (var i = 0; i < currentListView.Columns.Count; i++)
            {
                for (var j = 0; j < currentListView.Columns.Count; j++)
                {
                    if (currentListView.Columns[j].DisplayIndex == i)
                    {
                        dispOrder[i] = j;
                        break;
                    }
                }
            }

            //列幅、列並びを他のタブに設定
            foreach (TabPage tb in ListTab.TabPages)
            {
                if (tb.Text == this.CurrentTabName)
                    continue;

                if (tb.Tag != null && tb.Controls.Count > 0)
                {
                    var lst = (DetailsListView)tb.Tag;
                    for (var i = 0; i < lst.Columns.Count; i++)
                    {
                        lst.Columns[dispOrder[i]].DisplayIndex = i;
                        lst.Columns[i].Width = currentListView.Columns[i].Width;
                    }
                }
            }

            _isColumnChanged = false;
        }

        private void StatusText_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '@')
            {
                if (!SettingManager.Common.UseAtIdSupplement) return;
                //@マーク
                var cnt = AtIdSupl.ItemCount;
                ShowSuplDialog(StatusText, AtIdSupl);
                if (cnt != AtIdSupl.ItemCount)
                    this.MarkSettingAtIdModified();
                e.Handled = true;
            }
            else if (e.KeyChar == '#')
            {
                if (!SettingManager.Common.UseHashSupplement) return;
                ShowSuplDialog(StatusText, HashSupl);
                e.Handled = true;
            }
        }

        public void ShowSuplDialog(TextBox owner, AtIdSupplement dialog)
            => this.ShowSuplDialog(owner, dialog, 0, "");

        public void ShowSuplDialog(TextBox owner, AtIdSupplement dialog, int offset)
            => this.ShowSuplDialog(owner, dialog, offset, "");

        public void ShowSuplDialog(TextBox owner, AtIdSupplement dialog, int offset, string startswith)
        {
            dialog.StartsWith = startswith;
            if (dialog.Visible)
            {
                dialog.Focus();
            }
            else
            {
                dialog.ShowDialog();
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
            var selStart = owner.SelectionStart;
            var fHalf = "";
            var eHalf = "";
            if (dialog.DialogResult == DialogResult.OK)
            {
                if (!MyCommon.IsNullOrEmpty(dialog.inputText))
                {
                    if (selStart > 0)
                    {
                        fHalf = owner.Text.Substring(0, selStart - offset);
                    }
                    if (selStart < owner.Text.Length)
                    {
                        eHalf = owner.Text.Substring(selStart);
                    }
                    owner.Text = fHalf + dialog.inputText + eHalf;
                    owner.SelectionStart = selStart + dialog.inputText.Length;
                }
            }
            else
            {
                if (selStart > 0)
                {
                    fHalf = owner.Text.Substring(0, selStart);
                }
                if (selStart < owner.Text.Length)
                {
                    eHalf = owner.Text.Substring(selStart);
                }
                owner.Text = fHalf + eHalf;
                if (selStart > 0)
                {
                    owner.SelectionStart = selStart;
                }
            }
            owner.Focus();
        }

        private void StatusText_KeyUp(object sender, KeyEventArgs e)
        {
            //スペースキーで未読ジャンプ
            if (!e.Alt && !e.Control && !e.Shift)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.ProcessKey)
                {
                    var isSpace = false;
                    foreach (var c in StatusText.Text)
                    {
                        if (c == ' ' || c == '　')
                        {
                            isSpace = true;
                        }
                        else
                        {
                            isSpace = false;
                            break;
                        }
                    }
                    if (isSpace)
                    {
                        e.Handled = true;
                        StatusText.Text = "";
                        JumpUnreadMenuItem_Click(this.JumpUnreadMenuItem, EventArgs.Empty);
                    }
                }
            }
            this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);
        }

        private void StatusText_TextChanged(object sender, EventArgs e)
        {
            //文字数カウント
            var pLen = this.GetRestStatusCount(this.FormatStatusTextExtended(this.StatusText.Text));
            lblLen.Text = pLen.ToString();
            if (pLen < 0)
            {
                StatusText.ForeColor = Color.Red;
            }
            else
            {
                StatusText.ForeColor = _clInputFont;
            }

            this.StatusText.AccessibleDescription = string.Format(Properties.Resources.StatusText_AccessibleDescription, pLen);

            if (MyCommon.IsNullOrEmpty(StatusText.Text))
            {
                this.inReplyTo = null;
            }
        }

        /// <summary>
        /// メンション以外の文字列が含まれていないテキストであるか判定します
        /// </summary>
        internal static bool TextContainsOnlyMentions(string text)
        {
            var mentions = TweetExtractor.ExtractMentionEntities(text).OrderBy(x => x.Indices[0]);
            var startIndex = 0;

            foreach (var mention in mentions)
            {
                var textPart = text.Substring(startIndex, mention.Indices[0] - startIndex);

                if (!string.IsNullOrWhiteSpace(textPart))
                    return false;

                startIndex = mention.Indices[1];
            }

            var textPartLast = text.Substring(startIndex);

            if (!string.IsNullOrWhiteSpace(textPartLast))
                return false;

            return true;
        }

        /// <summary>
        /// 投稿時に auto_populate_reply_metadata オプションによって自動で追加されるメンションを除去します
        /// </summary>
        private string RemoveAutoPopuratedMentions(string statusText, out long[] autoPopulatedUserIds)
        {
            var _autoPopulatedUserIds = new List<long>();

            var replyToPost = this.inReplyTo != null ? this._statuses[this.inReplyTo.Value.StatusId] : null;
            if (replyToPost != null)
            {
                if (statusText.StartsWith($"@{replyToPost.ScreenName} ", StringComparison.Ordinal))
                {
                    statusText = statusText.Substring(replyToPost.ScreenName.Length + 2);
                    _autoPopulatedUserIds.Add(replyToPost.UserId);

                    foreach (var (userId, screenName) in replyToPost.ReplyToList)
                    {
                        if (statusText.StartsWith($"@{screenName} ", StringComparison.Ordinal))
                        {
                            statusText = statusText.Substring(screenName.Length + 2);
                            _autoPopulatedUserIds.Add(userId);
                        }
                    }
                }
            }

            autoPopulatedUserIds = _autoPopulatedUserIds.ToArray();

            return statusText;
        }

        /// <summary>
        /// attachment_url に指定可能な URL が含まれていれば除去
        /// </summary>
        private string RemoveAttachmentUrl(string statusText, out string? attachmentUrl)
        {
            attachmentUrl = null;

            // attachment_url は media_id と同時に使用できない
            if (this.ImageSelector.Visible && this.ImageSelector.SelectedService is TwitterPhoto)
                return statusText;

            var match = Twitter.AttachmentUrlRegex.Match(statusText);
            if (!match.Success)
                return statusText;

            attachmentUrl = match.Value;

            // マッチした URL を空白に置換
            statusText = statusText.Substring(0, match.Index);

            // テキストと URL の間にスペースが含まれていれば除去
            return statusText.TrimEnd(' ');
        }

        private string FormatStatusTextExtended(string statusText)
            => this.FormatStatusTextExtended(statusText, out _, out _);

        /// <summary>
        /// <see cref="FormatStatusText"/> に加えて、拡張モードで140字にカウントされない文字列の除去を行います
        /// </summary>
        private string FormatStatusTextExtended(string statusText, out long[] autoPopulatedUserIds, out string? attachmentUrl)
        {
            statusText = this.RemoveAutoPopuratedMentions(statusText, out autoPopulatedUserIds);

            statusText = this.RemoveAttachmentUrl(statusText, out attachmentUrl);

            return this.FormatStatusText(statusText);
        }

        /// <summary>
        /// ツイート投稿前のフッター付与などの前処理を行います
        /// </summary>
        private string FormatStatusText(string statusText)
        {
            statusText = statusText.Replace("\r\n", "\n");

            if (this.urlMultibyteSplit)
            {
                // URLと全角文字の切り離し
                statusText = Regex.Replace(statusText, @"https?:\/\/[-_.!~*'()a-zA-Z0-9;\/?:\@&=+\$,%#^]+", "$& ");
            }

            if (SettingManager.Common.WideSpaceConvert)
            {
                // 文中の全角スペースを半角スペース1個にする
                statusText = statusText.Replace("　", " ");
            }

            // DM の場合はこれ以降の処理を行わない
            if (statusText.StartsWith("D ", StringComparison.OrdinalIgnoreCase))
                return statusText;

            bool disableFooter;
            if (SettingManager.Common.PostShiftEnter)
            {
                disableFooter = MyCommon.IsKeyDown(Keys.Control);
            }
            else
            {
                if (this.StatusText.Multiline && !SettingManager.Common.PostCtrlEnter)
                    disableFooter = MyCommon.IsKeyDown(Keys.Control);
                else
                    disableFooter = MyCommon.IsKeyDown(Keys.Shift);
            }

            if (statusText.Contains("RT @"))
                disableFooter = true;

            // 自分宛のリプライの場合は先頭の「@screen_name 」の部分を除去する (in_reply_to_status_id は維持される)
            if (this.inReplyTo != null && this.inReplyTo.Value.ScreenName == this.tw.Username)
            {
                var mentionSelf = $"@{this.tw.Username} ";
                if (statusText.StartsWith(mentionSelf, StringComparison.OrdinalIgnoreCase))
                {
                    if (statusText.Length > mentionSelf.Length || this.GetSelectedImageService() != null)
                        statusText = statusText.Substring(mentionSelf.Length);
                }
            }

            var header = "";
            var footer = "";

            var hashtag = this.HashMgr.UseHash;
            if (!MyCommon.IsNullOrEmpty(hashtag) && !(this.HashMgr.IsNotAddToAtReply && this.inReplyTo != null))
            {
                if (HashMgr.IsHead)
                    header = HashMgr.UseHash + " ";
                else
                    footer = " " + HashMgr.UseHash;
            }

            if (!disableFooter)
            {
                if (SettingManager.Local.UseRecommendStatus)
                {
                    // 推奨ステータスを使用する
                    footer += this.recommendedStatusFooter;
                }
                else if (!MyCommon.IsNullOrEmpty(SettingManager.Local.StatusText))
                {
                    // テキストボックスに入力されている文字列を使用する
                    footer += " " + SettingManager.Local.StatusText.Trim();
                }
            }

            statusText = header + statusText + footer;

            if (this.preventSmsCommand)
            {
                // ツイートが意図せず SMS コマンドとして解釈されることを回避 (D, DM, M のみ)
                // 参照: https://support.twitter.com/articles/14020

                if (Regex.IsMatch(statusText, @"^[+\-\[\]\s\\.,*/(){}^~|='&%$#""<>?]*(d|dm|m)([+\-\[\]\s\\.,*/(){}^~|='&%$#""<>?]+|$)", RegexOptions.IgnoreCase)
                    && !Twitter.DMSendTextRegex.IsMatch(statusText))
                {
                    // U+200B (ZERO WIDTH SPACE) を先頭に加えて回避
                    statusText = '\u200b' + statusText;
                }
            }

            return statusText;
        }

        /// <summary>
        /// 投稿欄に表示する入力可能な文字数を計算します
        /// </summary>
        private int GetRestStatusCount(string statusText)
        {
            var remainCount = this.tw.GetTextLengthRemain(statusText);

            var uploadService = this.GetSelectedImageService();
            if (uploadService != null)
            {
                // TODO: ImageSelector で選択中の画像の枚数が mediaCount 引数に渡るようにする
                remainCount -= uploadService.GetReservedTextLength(1);
            }

            return remainCount;
        }

        private IMediaUploadService? GetSelectedImageService()
            => this.ImageSelector.Visible ? this.ImageSelector.SelectedService : null;

        private void MyList_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            if (sender != this.CurrentListView)
                return;

            var listCache = this._listItemCache;
            if (listCache?.TargetList == sender && listCache.IsSupersetOf(e.StartIndex, e.EndIndex))
            {
                // If the newly requested cache is a subset of the old cache,
                // no need to rebuild everything, so do nothing.
                return;
            }

            // Now we need to rebuild the cache.
            this.CreateCache(e.StartIndex, e.EndIndex);
        }

        private void MyList_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var listCache = this._listItemCache;
            if (listCache?.TargetList == sender)
            {
                if (listCache.TryGetValue(e.ItemIndex, out var item, out _))
                {
                    e.Item = item;
                    return;
                }
            }

            // A cache miss, so create a new ListViewItem and pass it back.
            var tabPage = (TabPage)((DetailsListView)sender).Parent;
            var tab = this._statuses.Tabs[tabPage.Text];
            try
            {
                e.Item = this.CreateItem(tab, tab[e.ItemIndex]);
            }
            catch (Exception)
            {
                // 不正な要求に対する間に合わせの応答
                string[] sitem = {"", "", "", "", "", "", "", ""};
                e.Item = new ImageListViewItem(sitem);
            }
        }

        private void CreateCache(int startIndex, int endIndex)
        {
            var tabInfo = this.CurrentTab;

            if (tabInfo.AllCount == 0)
                return;

            // インデックスを 0...(tabInfo.AllCount - 1) の範囲内にする
            int FilterRange(int index)
                => Math.Max(Math.Min(index, tabInfo.AllCount - 1), 0);

            // キャッシュ要求（要求範囲±30を作成）
            startIndex = FilterRange(startIndex - 30);
            endIndex = FilterRange(endIndex + 30);

            var cacheLength = endIndex - startIndex + 1;

            var tab = this.CurrentTab;
            var posts = tabInfo[startIndex, endIndex]; //配列で取得
            var listItems = Enumerable.Range(0, cacheLength)
                .Select(x => this.CreateItem(tab, posts[x]))
                .ToArray();

            var listCache = new ListViewItemCache
            {
                TargetList = this.CurrentListView,
                StartIndex = startIndex,
                EndIndex = endIndex,
                Cache = Enumerable.Zip(listItems, posts, (x, y) => (x, y)).ToArray(),
            };

            Interlocked.Exchange(ref this._listItemCache, listCache);
        }

        /// <summary>
        /// DetailsListView のための ListViewItem のキャッシュを消去する
        /// </summary>
        private void PurgeListViewItemCache()
            => Interlocked.Exchange(ref this._listItemCache, null);

        private ListViewItem CreateItem(TabModel tab, PostClass Post)
        {
            var mk = new StringBuilder();

            if (Post.FavoritedCount > 0) mk.Append("+" + Post.FavoritedCount);
            ImageListViewItem itm;
            if (Post.RetweetedId == null)
            {
                string[] sitem= {"",
                                 Post.Nickname,
                                 Post.IsDeleted ? "(DELETED)" : Post.AccessibleText.Replace('\n', ' '),
                                 Post.CreatedAt.ToLocalTimeString(SettingManager.Common.DateTimeFormat),
                                 Post.ScreenName,
                                 "",
                                 mk.ToString(),
                                 Post.Source};
                itm = new ImageListViewItem(sitem, this.IconCache, Post.ImageUrl);
            }
            else
            {
                string[] sitem = {"",
                                  Post.Nickname,
                                  Post.IsDeleted ? "(DELETED)" : Post.AccessibleText.Replace('\n', ' '),
                                  Post.CreatedAt.ToLocalTimeString(SettingManager.Common.DateTimeFormat),
                                  Post.ScreenName + Environment.NewLine + "(RT:" + Post.RetweetedBy + ")",
                                  "",
                                  mk.ToString(),
                                  Post.Source};
                itm = new ImageListViewItem(sitem, this.IconCache, Post.ImageUrl);
            }
            itm.StateIndex = Post.StateIndex;
            itm.Tag = Post;

            var read = Post.IsRead;
            // 未読管理していなかったら既読として扱う
            if (!tab.UnreadManage || !SettingManager.Common.UnreadManage)
                read = true;

            ChangeItemStyleRead(read, itm, Post, null);

            if (tab.TabName == this.CurrentTabName)
                this.ColorizeList(itm, Post);

            return itm;
        }

        /// <summary>
        /// 全てのタブの振り分けルールを反映し直します
        /// </summary>
        private void ApplyPostFilters()
        {
            using (ControlTransaction.Cursor(this, Cursors.WaitCursor))
            {
                this.PurgeListViewItemCache();
                this._statuses.FilterAll();

                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    var tabPage = this.ListTab.TabPages[index];
                    var listview = (DetailsListView)tabPage.Tag;
                    using (ControlTransaction.Update(listview))
                    {
                        listview.VirtualListSize = tab.AllCount;
                    }

                    if (SettingManager.Common.TabIconDisp)
                    {
                        if (tab.UnreadCount > 0)
                            tabPage.ImageIndex = 0;
                        else
                            tabPage.ImageIndex = -1;
                    }
                }

                if (!SettingManager.Common.TabIconDisp)
                    this.ListTab.Refresh();

                SetMainWindowTitle();
                SetStatusLabelUrl();
            }
        }

        private void MyList_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
            => e.DrawDefault = true;

        private void MyList_HScrolled(object sender, EventArgs e)
            => ((DetailsListView)sender).Refresh();

        private void MyList_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (e.State == 0) return;
            e.DrawDefault = false;

            SolidBrush brs2;
            if (!e.Item.Selected)     //e.ItemStateでうまく判定できない？？？
            {
                if (e.Item.BackColor == _clSelf)
                    brs2 = _brsBackColorMine;
                else if (e.Item.BackColor == _clAtSelf)
                    brs2 = _brsBackColorAt;
                else if (e.Item.BackColor == _clTarget)
                    brs2 = _brsBackColorYou;
                else if (e.Item.BackColor == _clAtTarget)
                    brs2 = _brsBackColorAtYou;
                else if (e.Item.BackColor == _clAtFromTarget)
                    brs2 = _brsBackColorAtFromTarget;
                else if (e.Item.BackColor == _clAtTo)
                    brs2 = _brsBackColorAtTo;
                else
                    brs2 = _brsBackColorNone;
            }
            else
            {
                //選択中の行
                if (((Control)sender).Focused)
                    brs2 = _brsHighLight;
                else
                    brs2 = _brsDeactiveSelection;
            }
            e.Graphics.FillRectangle(brs2, e.Bounds);
            e.DrawFocusRectangle();
            this.DrawListViewItemIcon(e);
        }

        private void MyList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ItemState == 0) return;

            if (e.ColumnIndex > 0)
            {
                //アイコン以外の列
                var post = (PostClass)e.Item.Tag;

                RectangleF rct = e.Bounds;
                rct.Width = e.Header.Width;
                var fontHeight = e.Item.Font.Height;
                if (_iconCol)
                {
                    rct.Y += fontHeight;
                    rct.Height -= fontHeight;
                }

                var drawLineCount = Math.Max(1, Math.DivRem((int)rct.Height, fontHeight, out var heightDiff));

                //フォントの高さの半分を足してるのは保険。無くてもいいかも。
                if (this._iconCol || drawLineCount > 1)
                {
                    if (heightDiff < fontHeight * 0.7)
                    {
                        // 最終行が70%以上欠けていたら、最終行は表示しない
                        rct.Height = (fontHeight * drawLineCount) - 1;
                    }
                    else
                    {
                        drawLineCount += 1;
                    }
                }

                if (rct.Width > 0)
                {
                    var color = (!e.Item.Selected) ? e.Item.ForeColor :   //選択されていない行
                        (((Control)sender).Focused) ? _clHighLight :        //選択中の行
                        _clUnread;

                    if (_iconCol)
                    {
                        var rctB = e.Bounds;
                        rctB.Width = e.Header.Width;
                        rctB.Height = fontHeight;

                        using var fnt = new Font(e.Item.Font, FontStyle.Bold);

                        TextRenderer.DrawText(e.Graphics,
                            post.IsDeleted ? "(DELETED)" : post.TextSingleLine,
                            e.Item.Font,
                            Rectangle.Round(rct),
                            color,
                            TextFormatFlags.WordBreak |
                            TextFormatFlags.EndEllipsis |
                            TextFormatFlags.GlyphOverhangPadding |
                            TextFormatFlags.NoPrefix);
                        TextRenderer.DrawText(e.Graphics,
                            e.Item.SubItems[4].Text + " / " + e.Item.SubItems[1].Text + " (" + e.Item.SubItems[3].Text + ") " + e.Item.SubItems[5].Text + e.Item.SubItems[6].Text + " [" + e.Item.SubItems[7].Text + "]",
                            fnt,
                            rctB,
                            color,
                            TextFormatFlags.SingleLine |
                            TextFormatFlags.EndEllipsis |
                            TextFormatFlags.GlyphOverhangPadding |
                            TextFormatFlags.NoPrefix);
                    }
                    else
                    {
                        string text;
                        if (e.ColumnIndex != 2)
                            text = e.SubItem.Text;
                        else
                            text = post.IsDeleted ? "(DELETED)" : post.TextSingleLine;

                        if (drawLineCount == 1)
                        {
                            TextRenderer.DrawText(e.Graphics,
                                                    text,
                                                    e.Item.Font,
                                                    Rectangle.Round(rct),
                                                    color,
                                                    TextFormatFlags.SingleLine |
                                                    TextFormatFlags.EndEllipsis |
                                                    TextFormatFlags.GlyphOverhangPadding |
                                                    TextFormatFlags.NoPrefix |
                                                    TextFormatFlags.VerticalCenter);
                        }
                        else
                        {
                            TextRenderer.DrawText(e.Graphics,
                                                    text,
                                                    e.Item.Font,
                                                    Rectangle.Round(rct),
                                                    color,
                                                    TextFormatFlags.WordBreak |
                                                    TextFormatFlags.EndEllipsis |
                                                    TextFormatFlags.GlyphOverhangPadding |
                                                    TextFormatFlags.NoPrefix);
                        }
                    }
                }
            }
        }

        private void DrawListViewItemIcon(DrawListViewItemEventArgs e)
        {
            if (_iconSz == 0) return;

            var item = (ImageListViewItem)e.Item;

            //e.Bounds.Leftが常に0を指すから自前で計算
            var itemRect = item.Bounds;
            var col0 = e.Item.ListView.Columns[0];
            itemRect.Width = col0.Width;

            if (col0.DisplayIndex > 0)
            {
                foreach (ColumnHeader clm in e.Item.ListView.Columns)
                {
                    if (clm.DisplayIndex < col0.DisplayIndex)
                        itemRect.X += clm.Width;
                }
            }

            // ディスプレイの DPI 設定を考慮したアイコンサイズ
            var realIconSize = new SizeF(this._iconSz * this.CurrentScaleFactor.Width, this._iconSz * this.CurrentScaleFactor.Height).ToSize();
            var realStateSize = new SizeF(16 * this.CurrentScaleFactor.Width, 16 * this.CurrentScaleFactor.Height).ToSize();

            Rectangle iconRect;
            var img = item.Image;
            if (img != null)
            {
                iconRect = Rectangle.Intersect(new Rectangle(e.Item.GetBounds(ItemBoundsPortion.Icon).Location, realIconSize), itemRect);
                iconRect.Offset(0, Math.Max(0, (itemRect.Height - realIconSize.Height) / 2));

                if (iconRect.Width > 0)
                {
                    e.Graphics.FillRectangle(Brushes.White, iconRect);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    try
                    {
                        e.Graphics.DrawImage(img.Image, iconRect);
                    }
                    catch (ArgumentException)
                    {
                        item.RefreshImageAsync();
                    }
                }
            }
            else
            {
                iconRect = Rectangle.Intersect(new Rectangle(e.Item.GetBounds(ItemBoundsPortion.Icon).Location, new Size(1, 1)), itemRect);

                item.GetImageAsync();
            }

            if (item.StateIndex > -1)
            {
                var stateRect = Rectangle.Intersect(new Rectangle(new Point(iconRect.X + realIconSize.Width + 2, iconRect.Y), realStateSize), itemRect);
                if (stateRect.Width > 0)
                    e.Graphics.DrawImage(this.PostStateImageList.Images[item.StateIndex], stateRect);
            }
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);

            ScaleChildControl(this.TabImage, factor);

            var tabpages = this.ListTab.TabPages.Cast<TabPage>();
            var listviews = tabpages.Select(x => x.Tag).Cast<ListView>();

            foreach (var listview in listviews)
            {
                ScaleChildControl(listview, factor);
            }
        }

        internal void DoTabSearch(string searchWord, bool caseSensitive, bool useRegex, SEARCHTYPE searchType)
        {
            var tab = this.CurrentTab;

            if (tab.AllCount == 0)
            {
                MessageBox.Show(Properties.Resources.DoTabSearchText2, Properties.Resources.DoTabSearchText3, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedIndex = tab.SelectedIndex;

            int startIndex;
            switch (searchType)
            {
                case SEARCHTYPE.NextSearch: // 次を検索
                    if (selectedIndex != -1)
                        startIndex = Math.Min(selectedIndex + 1, tab.AllCount - 1);
                    else
                        startIndex = 0;
                    break;
                case SEARCHTYPE.PrevSearch: // 前を検索
                    if (selectedIndex != -1)
                        startIndex = Math.Max(selectedIndex - 1, 0);
                    else
                        startIndex = tab.AllCount - 1;
                    break;
                case SEARCHTYPE.DialogSearch: // ダイアログからの検索
                default:
                    if (selectedIndex != -1)
                        startIndex = selectedIndex;
                    else
                        startIndex = 0;
                    break;
            }

            Func<string, bool> stringComparer;
            try
            {
                stringComparer = this.CreateSearchComparer(searchWord, useRegex, caseSensitive);
            }
            catch (ArgumentException)
            {
                MessageBox.Show(Properties.Resources.DoTabSearchText1, Properties.Resources.DoTabSearchText3, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var reverse = searchType == SEARCHTYPE.PrevSearch;
            var foundIndex = tab.SearchPostsAll(stringComparer, startIndex, reverse)
                .DefaultIfEmpty(-1).First();

            if (foundIndex == -1)
            {
                MessageBox.Show(Properties.Resources.DoTabSearchText2, Properties.Resources.DoTabSearchText3, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var listView = this.CurrentListView;
            this.SelectListItem(listView, foundIndex);
            listView.EnsureVisible(foundIndex);
        }

        private void MenuItemSubSearch_Click(object sender, EventArgs e)
            => this.ShowSearchDialog(); // 検索メニュー

        private void MenuItemSearchNext_Click(object sender, EventArgs e)
        {
            var previousSearch = this.SearchDialog.ResultOptions;
            if (previousSearch == null || previousSearch.Type != SearchWordDialog.SearchType.Timeline)
            {
                this.SearchDialog.Reset();
                this.ShowSearchDialog();
                return;
            }

            // 次を検索
            this.DoTabSearch(
                previousSearch.Query,
                previousSearch.CaseSensitive,
                previousSearch.UseRegex,
                SEARCHTYPE.NextSearch);
        }

        private void MenuItemSearchPrev_Click(object sender, EventArgs e)
        {
            var previousSearch = this.SearchDialog.ResultOptions;
            if (previousSearch == null || previousSearch.Type != SearchWordDialog.SearchType.Timeline)
            {
                this.SearchDialog.Reset();
                this.ShowSearchDialog();
                return;
            }

            // 前を検索
            this.DoTabSearch(
                previousSearch.Query,
                previousSearch.CaseSensitive,
                previousSearch.UseRegex,
                SEARCHTYPE.PrevSearch);
        }

        /// <summary>
        /// 検索ダイアログを表示し、検索を実行します
        /// </summary>
        private void ShowSearchDialog()
        {
            if (this.SearchDialog.ShowDialog(this) != DialogResult.OK)
            {
                this.TopMost = SettingManager.Common.AlwaysTop;
                return;
            }
            this.TopMost = SettingManager.Common.AlwaysTop;

            var searchOptions = this.SearchDialog.ResultOptions!;
            if (searchOptions.Type == SearchWordDialog.SearchType.Timeline)
            {
                if (searchOptions.NewTab)
                {
                    var tabName = Properties.Resources.SearchResults_TabName;

                    try
                    {
                        tabName = this._statuses.MakeTabName(tabName);
                    }
                    catch (TabException ex)
                    {
                        MessageBox.Show(this, ex.Message, ApplicationSettings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    var resultTab = new LocalSearchTabModel(tabName);
                    this.AddNewTab(resultTab, startup: false);
                    this._statuses.AddTab(resultTab);

                    var targetTab = this.CurrentTab;

                    Func<string, bool> stringComparer;
                    try
                    {
                        stringComparer = this.CreateSearchComparer(searchOptions.Query, searchOptions.UseRegex, searchOptions.CaseSensitive);
                    }
                    catch (ArgumentException)
                    {
                        MessageBox.Show(Properties.Resources.DoTabSearchText1, Properties.Resources.DoTabSearchText3, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var foundIndices = targetTab.SearchPostsAll(stringComparer).ToArray();
                    if (foundIndices.Length == 0)
                    {
                        MessageBox.Show(Properties.Resources.DoTabSearchText2, Properties.Resources.DoTabSearchText3, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var foundPosts = foundIndices.Select(x => targetTab[x]);
                    foreach (var post in foundPosts)
                    {
                        resultTab.AddPostQueue(post);
                    }

                    this._statuses.DistributePosts();
                    this.RefreshTimeline();

                    var tabIndex = this._statuses.Tabs.IndexOf(tabName);
                    this.ListTab.SelectedIndex = tabIndex;
                }
                else
                {
                    this.DoTabSearch(
                        searchOptions.Query,
                        searchOptions.CaseSensitive,
                        searchOptions.UseRegex,
                        SEARCHTYPE.DialogSearch);
                }
            }
            else if (searchOptions.Type == SearchWordDialog.SearchType.Public)
            {
                this.AddNewTabForSearch(searchOptions.Query);
            }
        }

        /// <summary>発言検索に使用するメソッドを生成します</summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="useRegex"/> が true かつ、<paramref name="query"/> が不正な正規表現な場合
        /// </exception>
        private Func<string, bool> CreateSearchComparer(string query, bool useRegex, bool caseSensitive)
        {
            if (useRegex)
            {
                var regexOption = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(query, regexOption);

                return x => regex.IsMatch(x);
            }
            else
            {
                var comparisonType = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

                return x => x.IndexOf(query, comparisonType) != -1;
            }
        }

        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            using (var about = new TweenAboutBox())
            {
                about.ShowDialog(this);
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
        }

        private void JumpUnreadMenuItem_Click(object sender, EventArgs e)
        {
            var bgnIdx = this._statuses.SelectedTabIndex;

            if (ImageSelector.Enabled)
                return;

            TabModel? foundTab = null;
            var foundIndex = 0;

            //現在タブから最終タブまで探索
            foreach (var (tab, index) in this._statuses.Tabs.WithIndex().Skip(bgnIdx))
            {
                var unreadIndex = tab.NextUnreadIndex;
                if (unreadIndex != -1)
                {
                    ListTab.SelectedIndex = index;
                    foundTab = tab;
                    foundIndex = unreadIndex;
                    break;
                }
            }

            //未読みつからず＆現在タブが先頭ではなかったら、先頭タブから現在タブの手前まで探索
            if (foundTab == null && bgnIdx > 0)
            {
                foreach (var (tab, index) in this._statuses.Tabs.WithIndex().Take(bgnIdx))
                {
                    var unreadIndex = tab.NextUnreadIndex;
                    if (unreadIndex != -1)
                    {
                        ListTab.SelectedIndex = index;
                        foundTab = tab;
                        foundIndex = unreadIndex;
                        break;
                    }
                }
            }

            DetailsListView lst;

            if (foundTab == null)
            {
                //全部調べたが未読見つからず→先頭タブの最新発言へ
                ListTab.SelectedIndex = 0;
                var tabPage = this.ListTab.TabPages[0];
                var tab = this._statuses.Tabs[0];

                if (tab.AllCount == 0)
                    return;

                if (_statuses.SortOrder == SortOrder.Ascending)
                    foundIndex = tab.AllCount - 1;
                else
                    foundIndex = 0;

                lst = (DetailsListView)tabPage.Tag;
            }
            else
            {
                var foundTabIndex = this._statuses.Tabs.IndexOf(foundTab);
                lst = (DetailsListView)this.ListTab.TabPages[foundTabIndex].Tag;
            }

            SelectListItem(lst, foundIndex);

            if (_statuses.SortMode == ComparerMode.Id)
            {
                if (_statuses.SortOrder == SortOrder.Ascending && lst.Items[foundIndex].Position.Y > lst.ClientSize.Height - _iconSz - 10 ||
                    _statuses.SortOrder == SortOrder.Descending && lst.Items[foundIndex].Position.Y < _iconSz + 10)
                {
                    MoveTop();
                }
                else
                {
                    lst.EnsureVisible(foundIndex);
                }
            }
            else
            {
                lst.EnsureVisible(foundIndex);
            }

            lst.Focus();
        }

        private async void StatusOpenMenuItem_Click(object sender, EventArgs e)
        {
            var tab = this.CurrentTab;
            var post = this.CurrentPost;
            if (post != null && tab.TabType != MyCommon.TabUsageType.DirectMessage)
                await this.OpenUriInBrowserAsync(MyCommon.GetStatusUrl(post));
        }

        private async void VerUpMenuItem_Click(object sender, EventArgs e)
            => await this.CheckNewVersion(false);

        private void RunTweenUp()
        {
            var pinfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = MyCommon.settingPath,
                FileName = Path.Combine(MyCommon.settingPath, "TweenUp3.exe"),
                Arguments = "\"" + Application.StartupPath + "\"",
            };

            try
            {
                Process.Start(pinfo);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to execute TweenUp3.exe.");
            }
        }

        public class VersionInfo
        {
            public Version Version { get; }
            public Uri DownloadUri { get; }
            public string ReleaseNote { get; }

            public VersionInfo(Version version, Uri downloadUri, string releaseNote)
                => (this.Version, this.DownloadUri, this.ReleaseNote) = (version, downloadUri, releaseNote);
        }

        /// <summary>
        /// OpenTween の最新バージョンの情報を取得します
        /// </summary>
        public async Task<VersionInfo> GetVersionInfoAsync()
        {
            var versionInfoUrl = new Uri(ApplicationSettings.VersionInfoUrl + "?" +
                DateTimeUtc.Now.ToString("yyMMddHHmmss") + Environment.TickCount);

            var responseText = await Networking.Http.GetStringAsync(versionInfoUrl)
                .ConfigureAwait(false);

            // 改行2つで前後パートを分割（前半がバージョン番号など、後半が詳細テキスト）
            var msgPart = responseText.Split(new[] { "\n\n", "\r\n\r\n" }, 2, StringSplitOptions.None);

            var msgHeader = msgPart[0].Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
            var msgBody = msgPart.Length == 2 ? msgPart[1] : "";

            msgBody = Regex.Replace(msgBody, "(?<!\r)\n", "\r\n"); // LF -> CRLF

            return new VersionInfo(
                version: Version.Parse(msgHeader[0]),
                downloadUri: new Uri(msgHeader[1]),
                releaseNote: msgBody
            );
        }

        private async Task CheckNewVersion(bool startup = false)
        {
            if (ApplicationSettings.VersionInfoUrl == null)
                return; // 更新チェック無効化

            try
            {
                var versionInfo = await this.GetVersionInfoAsync();

                if (versionInfo.Version <= Version.Parse(MyCommon.FileVersion))
                {
                    // 更新不要
                    if (!startup)
                    {
                        var msgtext = string.Format(Properties.Resources.CheckNewVersionText7,
                            MyCommon.GetReadableVersion(), MyCommon.GetReadableVersion(versionInfo.Version));
                        msgtext = MyCommon.ReplaceAppName(msgtext);

                        MessageBox.Show(msgtext,
                            MyCommon.ReplaceAppName(Properties.Resources.CheckNewVersionText2),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                if (startup && versionInfo.Version <= SettingManager.Common.SkipUpdateVersion)
                    return;

                using var dialog = new UpdateDialog();

                dialog.SummaryText = string.Format(Properties.Resources.CheckNewVersionText3,
                    MyCommon.GetReadableVersion(versionInfo.Version));
                dialog.DetailsText = versionInfo.ReleaseNote;

                if (dialog.ShowDialog(this) == DialogResult.Yes)
                {
                    await this.OpenUriInBrowserAsync(versionInfo.DownloadUri.OriginalString);
                }
                else if (dialog.SkipButtonPressed)
                {
                    SettingManager.Common.SkipUpdateVersion = versionInfo.Version;
                    this.MarkSettingCommonModified();
                }
            }
            catch (Exception)
            {
                this.StatusLabel.Text = Properties.Resources.CheckNewVersionText9;
                if (!startup)
                {
                    MessageBox.Show(Properties.Resources.CheckNewVersionText10,
                        MyCommon.ReplaceAppName(Properties.Resources.CheckNewVersionText2),
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);
                }
            }
        }

        private void UpdateSelectedPost()
        {
            //件数関連の場合、タイトル即時書き換え
            if (SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.None &&
               SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.Post &&
               SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.Ver &&
               SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.OwnStatus)
            {
                SetMainWindowTitle();
            }
            if (!StatusLabelUrl.Text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                SetStatusLabelUrl();

            if (SettingManager.Common.TabIconDisp)
            {
                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    if (tab.UnreadCount == 0)
                    {
                        var tabPage = this.ListTab.TabPages[index];
                        if (tabPage.ImageIndex == 0)
                            tabPage.ImageIndex = -1;
                    }
                }
            }
            else
            {
                this.ListTab.Refresh();
            }

            this.DispSelectedPost();
        }

        public string createDetailHtml(string orgdata)
            => detailHtmlFormatHeader + orgdata + detailHtmlFormatFooter;

        private void DispSelectedPost()
            => this.DispSelectedPost(false);

        private PostClass displayPost = new PostClass();

        /// <summary>
        /// サムネイル表示に使用する CancellationToken の生成元
        /// </summary>
        private CancellationTokenSource? thumbnailTokenSource = null;

        private void DispSelectedPost(bool forceupdate)
        {
            var currentPost = this.CurrentPost;
            if (currentPost == null)
                return;

            var oldDisplayPost = this.displayPost;
            this.displayPost = currentPost;

            if (!forceupdate && currentPost.Equals(oldDisplayPost))
                return;

            var loadTasks = new List<Task>
            {
                this.tweetDetailsView.ShowPostDetails(currentPost),
            };

            this.SplitContainer3.Panel2Collapsed = true;

            if (SettingManager.Common.PreviewEnable)
            {
                var oldTokenSource = Interlocked.Exchange(ref this.thumbnailTokenSource, new CancellationTokenSource());
                oldTokenSource?.Cancel();

                var token = this.thumbnailTokenSource!.Token;
                loadTasks.Add(this.tweetThumbnail1.ShowThumbnailAsync(currentPost, token));
            }

            async Task delayedTasks()
            {
                try
                {
                    await Task.WhenAll(loadTasks);
                }
                catch (OperationCanceledException) { }
            }

            // サムネイルの読み込みを待たずに次に選択されたツイートを表示するため await しない
            _ = delayedTasks();
        }

        /// <summary>
        /// 画像詳細表示に使用する CancellationToken の生成元
        /// </summary>
        private CancellationTokenSource imageDetailTokenSource = null;
        public void ShowImageDetailBrowser()
        {
            var oldTokenSource = Interlocked.Exchange(ref this.imageDetailTokenSource, new CancellationTokenSource());
            oldTokenSource?.Cancel();

            var token = this.imageDetailTokenSource.Token;
            try
            {
                var browser = new ImageDetailBrowser(this.CurrentPost, token);
                browser.ShowDialog();
            }
            catch (InvalidOperationException e) // 途中で死んだ場合のオブジェクトを確実に殺す
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.Source + "が原因です。");
                imageDetailTokenSource?.Cancel();
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.Source + "が原因です。");
                imageDetailTokenSource?.Cancel();
            }
        }


        private async void MatomeMenuItem_Click(object sender, EventArgs e)
            => await this.OpenApplicationWebsite();

        private async Task OpenApplicationWebsite()
            => await this.OpenUriInBrowserAsync(ApplicationSettings.WebsiteUrl);

        private async void ShortcutKeyListMenuItem_Click(object sender, EventArgs e)
            => await this.OpenUriInBrowserAsync(ApplicationSettings.ShortcutKeyUrl);

        private async void ListTab_KeyDown(object sender, KeyEventArgs e)
        {
            var tab = this.CurrentTab;
            if (tab.TabType == MyCommon.TabUsageType.PublicSearch)
            {
                var pnl = this.CurrentTabPage.Controls["panelSearch"];
                if (pnl.Controls["comboSearch"].Focused ||
                    pnl.Controls["comboLang"].Focused ||
                    pnl.Controls["buttonSearch"].Focused) return;
            }

            if (e.Control || e.Shift || e.Alt)
                this._anchorFlag = false;

            if (CommonKeyDown(e.KeyData, FocusedControl.ListTab, out var asyncTask))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (asyncTask != null)
                await asyncTask;
        }

        private ShortcutCommand[] shortcutCommands = Array.Empty<ShortcutCommand>();

        private void InitializeShortcuts()
        {
            this.shortcutCommands = new[]
            {
                // リストのカーソル移動関係（上下キー、PageUp/Downに該当）
                ShortcutCommand.Create(Keys.J, Keys.Control | Keys.J, Keys.Shift | Keys.J, Keys.Control | Keys.Shift | Keys.J)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => SendKeys.Send("{DOWN}")),

                ShortcutCommand.Create(Keys.K, Keys.Control | Keys.K, Keys.Shift | Keys.K, Keys.Control | Keys.Shift | Keys.K)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => SendKeys.Send("{UP}")),

                ShortcutCommand.Create(Keys.F, Keys.Shift | Keys.F)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => SendKeys.Send("{PGDN}")),

                ShortcutCommand.Create(Keys.B, Keys.Shift | Keys.B)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => SendKeys.Send("{PGUP}")),

                ShortcutCommand.Create(Keys.F1)
                    .Do(() => this.OpenApplicationWebsite()),

                ShortcutCommand.Create(Keys.F3)
                    .Do(() => this.MenuItemSearchNext_Click(this.MenuItemSearchNext, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.F5)
                    .Do(() => this.DoRefresh()),

                ShortcutCommand.Create(Keys.F6)
                    .Do(() => this.RefreshTabAsync<MentionsTabModel>()),

                ShortcutCommand.Create(Keys.F7)
                    .Do(() => this.RefreshTabAsync<DirectMessagesTabModel>()),

                ShortcutCommand.Create(Keys.Space, Keys.ProcessKey)
                    .NotFocusedOn(FocusedControl.StatusText)
                    .Do(() => { this._anchorFlag = false; this.JumpUnreadMenuItem_Click(this.JumpUnreadMenuItem, EventArgs.Empty); }),

                ShortcutCommand.Create(Keys.G)
                    .NotFocusedOn(FocusedControl.StatusText)
                    .Do(() => { this._anchorFlag = false; this.ShowRelatedStatusesMenuItem_Click(this.ShowRelatedStatusesMenuItem, EventArgs.Empty); }),

                ShortcutCommand.Create(Keys.Right, Keys.N)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoRelPost(forward: true)),

                ShortcutCommand.Create(Keys.Left, Keys.P)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoRelPost(forward: false)),

                ShortcutCommand.Create(Keys.OemPeriod)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoAnchor()),

                ShortcutCommand.Create(Keys.I)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this.StatusText.Enabled)
                    .Do(() => this.StatusText.Focus()),

                ShortcutCommand.Create(Keys.Enter)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.MakeReplyOrDirectStatus()),

                ShortcutCommand.Create(Keys.R)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.DoRefresh()),

                ShortcutCommand.Create(Keys.L)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.GoPost(forward: true); }),

                ShortcutCommand.Create(Keys.H)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.GoPost(forward: false); }),

                ShortcutCommand.Create(Keys.Z, Keys.Oemcomma)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.MoveTop(); }),

                ShortcutCommand.Create(Keys.S)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.GoNextTab(forward: true); }),

                ShortcutCommand.Create(Keys.A)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.GoNextTab(forward: false); }),

                // ] in_reply_to参照元へ戻る
                ShortcutCommand.Create(Keys.Oem4)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; return this.GoInReplyToPostTree(); }),

                // [ in_reply_toへジャンプ
                ShortcutCommand.Create(Keys.Oem6)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => { this._anchorFlag = false; this.GoBackInReplyToPostTree(); }),

                ShortcutCommand.Create(Keys.Escape)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => {
                        this._anchorFlag = false;
                        var tab = this.CurrentTab;
                        var tabtype = tab.TabType;
                        if (tabtype == MyCommon.TabUsageType.Related || tabtype == MyCommon.TabUsageType.UserTimeline || tabtype == MyCommon.TabUsageType.PublicSearch || tabtype == MyCommon.TabUsageType.SearchResults)
                        {
                            RemoveSpecifiedTab(tab.TabName, false);
                            SaveConfigsTabs();
                        }
                    }),

                // 上下キー, PageUp/Downキー, Home/Endキー は既定の動作を残しつつアンカー初期化
                ShortcutCommand.Create(Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this._anchorFlag = false, preventDefault: false),

                // PreviewKeyDownEventArgs.IsInputKey を true にしてスクロールを発生させる
                ShortcutCommand.Create(Keys.Up, Keys.Down)
                    .FocusedOn(FocusedControl.PostBrowser)
                    .Do(() => { }),

                ShortcutCommand.Create(Keys.Control | Keys.R)
                    .Do(() => this.MakeReplyOrDirectStatus(isAuto: false, isReply: true)),

                ShortcutCommand.Create(Keys.Control | Keys.D)
                    .Do(() => this.doStatusDelete()),

                ShortcutCommand.Create(Keys.Control | Keys.M)
                    .Do(() => this.MakeReplyOrDirectStatus(isAuto: false, isReply: false)),

                ShortcutCommand.Create(Keys.Control | Keys.S)
                    .Do(() => this.FavoriteChange(FavAdd: true)),

                ShortcutCommand.Create(Keys.Control | Keys.I)
                    .Do(() => this.doRepliedStatusOpen()),

                ShortcutCommand.Create(Keys.Control | Keys.Q)
                    .Do(() => this.doQuoteOfficial()),

                ShortcutCommand.Create(Keys.Control | Keys.B)
                    .Do(() => this.ReadedStripMenuItem_Click(this.ReadedStripMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.T)
                    .Do(() => this.HashManageMenuItem_Click(this.HashManageMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.L)
                    .Do(() => this.UrlConvertAutoToolStripMenuItem_Click(this.UrlConvertAutoToolStripMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.Y)
                    .NotFocusedOn(FocusedControl.PostBrowser)
                    .Do(() => this.MultiLineMenuItem_Click(this.MultiLineMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.F)
                    .Do(() => this.MenuItemSubSearch_Click(this.MenuItemSubSearch, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.U)
                    .Do(() => this.ShowUserTimeline()),

                ShortcutCommand.Create(Keys.Control | Keys.H)
                    .Do(() => this.MoveToHomeToolStripMenuItem_Click(this.MoveToHomeToolStripMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.G)
                    .Do(() => this.MoveToFavToolStripMenuItem_Click(this.MoveToFavToolStripMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.O)
                    .Do(() => this.StatusOpenMenuItem_Click(this.StatusOpenMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.E)
                    .Do(() => this.OpenURLMenuItem_Click(this.OpenURLMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.Home, Keys.Control | Keys.End)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.selectionDebouncer.Call(), preventDefault: false),

                ShortcutCommand.Create(Keys.Control | Keys.N)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoNextTab(forward: true)),

                ShortcutCommand.Create(Keys.Control | Keys.P)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoNextTab(forward: false)),

                ShortcutCommand.Create(Keys.Control | Keys.C, Keys.Control | Keys.Insert)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.CopyStot()),

                // タブダイレクト選択(Ctrl+1～8,Ctrl+9)
                ShortcutCommand.Create(Keys.Control | Keys.D1)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 1)
                    .Do(() => this.ListTab.SelectedIndex = 0),

                ShortcutCommand.Create(Keys.Control | Keys.D2)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 2)
                    .Do(() => this.ListTab.SelectedIndex = 1),

                ShortcutCommand.Create(Keys.Control | Keys.D3)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 3)
                    .Do(() => this.ListTab.SelectedIndex = 2),

                ShortcutCommand.Create(Keys.Control | Keys.D4)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 4)
                    .Do(() => this.ListTab.SelectedIndex = 3),

                ShortcutCommand.Create(Keys.Control | Keys.D5)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 5)
                    .Do(() => this.ListTab.SelectedIndex = 4),

                ShortcutCommand.Create(Keys.Control | Keys.D6)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 6)
                    .Do(() => this.ListTab.SelectedIndex = 5),

                ShortcutCommand.Create(Keys.Control | Keys.D7)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 7)
                    .Do(() => this.ListTab.SelectedIndex = 6),

                ShortcutCommand.Create(Keys.Control | Keys.D8)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this._statuses.Tabs.Count >= 8)
                    .Do(() => this.ListTab.SelectedIndex = 7),

                ShortcutCommand.Create(Keys.Control | Keys.D9)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.ListTab.SelectedIndex = this._statuses.Tabs.Count - 1),

                ShortcutCommand.Create(Keys.Control | Keys.A)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => this.StatusText.SelectAll()),

                ShortcutCommand.Create(Keys.Control | Keys.V)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => this.ProcClipboardFromStatusTextWhenCtrlPlusV()),

                ShortcutCommand.Create(Keys.Control | Keys.Up)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => this.StatusTextHistoryBack()),

                ShortcutCommand.Create(Keys.Control | Keys.Down)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => this.StatusTextHistoryForward()),

                ShortcutCommand.Create(Keys.Control | Keys.PageUp, Keys.Control | Keys.P)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => {
                        if (ListTab.SelectedIndex == 0)
                        {
                            ListTab.SelectedIndex = ListTab.TabCount - 1;
                        }
                        else
                        {
                            ListTab.SelectedIndex -= 1;
                        }
                        StatusText.Focus();
                    }),

                ShortcutCommand.Create(Keys.Control | Keys.PageDown, Keys.Control | Keys.N)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => {
                        if (ListTab.SelectedIndex == ListTab.TabCount - 1)
                        {
                            ListTab.SelectedIndex = 0;
                        }
                        else
                        {
                            ListTab.SelectedIndex += 1;
                        }
                        StatusText.Focus();
                    }),

                ShortcutCommand.Create(Keys.Control | Keys.Y)
                    .FocusedOn(FocusedControl.PostBrowser)
                    .Do(() => {
                        var multiline = !SettingManager.Local.StatusMultiline;
                        SettingManager.Local.StatusMultiline = multiline;
                        MultiLineMenuItem.Checked = multiline;
                        MultiLineMenuItem_Click(this.MultiLineMenuItem, EventArgs.Empty);
                    }),

                ShortcutCommand.Create(Keys.Shift | Keys.F3)
                    .Do(() => this.MenuItemSearchPrev_Click(this.MenuItemSearchPrev, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Shift | Keys.F5)
                    .Do(() => this.DoRefreshMore()),

                ShortcutCommand.Create(Keys.Shift | Keys.F6)
                    .Do(() => this.RefreshTabAsync<MentionsTabModel>(backward: true)),

                ShortcutCommand.Create(Keys.Shift | Keys.F7)
                    .Do(() => this.RefreshTabAsync<DirectMessagesTabModel>(backward: true)),

                ShortcutCommand.Create(Keys.Shift | Keys.R)
                    .NotFocusedOn(FocusedControl.StatusText)
                    .Do(() => this.DoRefreshMore()),

                ShortcutCommand.Create(Keys.Shift | Keys.H)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoTopEnd(GoTop: true)),

                ShortcutCommand.Create(Keys.Shift | Keys.L)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoTopEnd(GoTop: false)),

                ShortcutCommand.Create(Keys.Shift | Keys.M)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoMiddle()),

                ShortcutCommand.Create(Keys.Shift | Keys.G)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoLast()),

                ShortcutCommand.Create(Keys.Shift | Keys.Z)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.MoveMiddle()),

                ShortcutCommand.Create(Keys.Shift | Keys.Oem4)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoBackInReplyToPostTree(parallel: true, isForward: false)),

                ShortcutCommand.Create(Keys.Shift | Keys.Oem6)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoBackInReplyToPostTree(parallel: true, isForward: true)),

                // お気に入り前後ジャンプ(SHIFT+N←/P→)
                ShortcutCommand.Create(Keys.Shift | Keys.Right, Keys.Shift | Keys.N)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoFav(forward: true)),

                // お気に入り前後ジャンプ(SHIFT+N←/P→)
                ShortcutCommand.Create(Keys.Shift | Keys.Left, Keys.Shift | Keys.P)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoFav(forward: false)),

                ShortcutCommand.Create(Keys.Shift | Keys.Space)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoBackSelectPostChain()),

                ShortcutCommand.Create(Keys.Alt | Keys.R)
                    .Do(() => this.doReTweetOfficial(isConfirm: true)),

                ShortcutCommand.Create(Keys.Alt | Keys.P)
                    .OnlyWhen(() => this.CurrentPost != null)
                    .Do(() => this.doShowUserStatus(this.CurrentPost!.ScreenName, ShowInputDialog: false)),

                ShortcutCommand.Create(Keys.Alt | Keys.Up)
                    .Do(() => this.tweetDetailsView.ScrollDownPostBrowser(forward: false)),

                ShortcutCommand.Create(Keys.Alt | Keys.Down)
                    .Do(() => this.tweetDetailsView.ScrollDownPostBrowser(forward: true)),

                ShortcutCommand.Create(Keys.Alt | Keys.PageUp)
                    .Do(() => this.tweetDetailsView.PageDownPostBrowser(forward: false)),

                ShortcutCommand.Create(Keys.Alt | Keys.PageDown)
                    .Do(() => this.tweetDetailsView.PageDownPostBrowser(forward: true)),

                // 別タブの同じ書き込みへ(ALT+←/→)
                ShortcutCommand.Create(Keys.Alt | Keys.Right)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoSamePostToAnotherTab(left: false)),

                ShortcutCommand.Create(Keys.Alt | Keys.Left)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.GoSamePostToAnotherTab(left: true)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.R)
                    .Do(() => this.MakeReplyOrDirectStatus(isAuto: false, isReply: true, isAll: true)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.C, Keys.Control | Keys.Shift | Keys.Insert)
                    .Do(() => this.CopyIdUri()),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.F)
                    .OnlyWhen(() => this.CurrentTab.TabType == MyCommon.TabUsageType.PublicSearch)
                    .Do(() => this.CurrentTabPage.Controls["panelSearch"].Controls["comboSearch"].Focus()),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.S)
                    .Do(() => this.FavoriteChange(FavAdd: false)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.B)
                    .Do(() => this.UnreadStripMenuItem_Click(this.UnreadStripMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.T)
                    .Do(() => this.HashToggleMenuItem_Click(this.HashToggleMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.P)
                    .Do(() => this.ImageSelectMenuItem_Click(this.ImageSelectMenuItem, EventArgs.Empty)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.H)
                    .Do(() => this.doMoveToRTHome()),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.Up)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => {
                        var tab = this.CurrentTab;
                        var selectedIndex = tab.SelectedIndex;
                        if (selectedIndex != -1 && selectedIndex > 0)
                        {
                            var listView = this.CurrentListView;
                            var idx = selectedIndex - 1;
                            SelectListItem(listView, idx);
                            listView.EnsureVisible(idx);
                        }
                    }),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.Down)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => {
                        var tab = this.CurrentTab;
                        var selectedIndex = tab.SelectedIndex;
                        if (selectedIndex != -1 && selectedIndex < tab.AllCount - 1)
                        {
                            var listView = this.CurrentListView;
                            var idx = selectedIndex + 1;
                            SelectListItem(listView, idx);
                            listView.EnsureVisible(idx);
                        }
                    }),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.Space)
                    .FocusedOn(FocusedControl.StatusText)
                    .Do(() => {
                        if (StatusText.SelectionStart > 0)
                        {
                            var endidx = StatusText.SelectionStart - 1;
                            var startstr = "";
                            for (var i = StatusText.SelectionStart - 1; i >= 0; i--)
                            {
                                var c = StatusText.Text[i];
                                if (char.IsLetterOrDigit(c) || c == '_')
                                {
                                    continue;
                                }
                                if (c == '@')
                                {
                                    startstr = StatusText.Text.Substring(i + 1, endidx - i);
                                    var cnt = AtIdSupl.ItemCount;
                                    ShowSuplDialog(StatusText, AtIdSupl, startstr.Length + 1, startstr);
                                    if (AtIdSupl.ItemCount != cnt)
                                        this.MarkSettingAtIdModified();
                                }
                                else if (c == '#')
                                {
                                    startstr = StatusText.Text.Substring(i + 1, endidx - i);
                                    ShowSuplDialog(StatusText, HashSupl, startstr.Length + 1, startstr);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }),

                // ソートダイレクト選択(Ctrl+Shift+1～8,Ctrl+Shift+9)
                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D1)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(0)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D2)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(1)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D3)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(2)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D4)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(3)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D5)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(4)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D6)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(5)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D7)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(6)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D8)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortColumnByDisplayIndex(7)),

                ShortcutCommand.Create(Keys.Control | Keys.Shift | Keys.D9)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.SetSortLastColumn()),

                ShortcutCommand.Create(Keys.Control | Keys.Alt | Keys.S)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.FavoritesRetweetOfficial()),

                ShortcutCommand.Create(Keys.Control | Keys.Alt | Keys.R)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.FavoritesRetweetUnofficial()),

                ShortcutCommand.Create(Keys.Control | Keys.Alt | Keys.H)
                    .FocusedOn(FocusedControl.ListTab)
                    .Do(() => this.OpenUserAppointUrl()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.R)
                    .FocusedOn(FocusedControl.PostBrowser)
                    .Do(() => this.doReTweetUnofficial()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.T)
                    .OnlyWhen(() => this.ExistCurrentPost)
                    .Do(() => this.tweetDetailsView.DoTranslation()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.R)
                    .Do(() => this.doReTweetUnofficial()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.C, Keys.Alt | Keys.Shift | Keys.Insert)
                    .Do(() => this.CopyUserId()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.Up)
                    .Do(() => this.tweetThumbnail1.ScrollUp()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.Down)
                    .Do(() => this.tweetThumbnail1.ScrollDown()),

                ShortcutCommand.Create(Keys.Alt | Keys.Shift | Keys.Enter)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => !this.SplitContainer3.Panel2Collapsed)
                    .Do(() => this.OpenThumbnailPicture(this.tweetThumbnail1.Thumbnail)),
                    
                ShortcutCommand.Create(Keys.D, Keys.Control | Keys.Shift | Keys.Enter)
                    .FocusedOn(FocusedControl.ListTab)
                    .OnlyWhen(() => this.CurrentPost != null)
                    .OnlyWhen(() => !this.SplitContainer3.Panel2Collapsed && this.CurrentPost.Media.Count > 0)
                    .Do(() => this.BeginInvoke((MethodInvoker)(() => ShowImageDetailBrowser()))),    //CommonKeyDownが正常に動かないので、非同期に機能させる
            };
        }

        internal bool CommonKeyDown(Keys keyData, FocusedControl focusedOn, out Task? asyncTask)
        {
            // Task を返す非同期処理があれば asyncTask に代入する
            asyncTask = null;

            // ShortcutCommand に対応しているコマンドはここで処理される
            foreach (var command in this.shortcutCommands)
            {
                if (command.IsMatch(keyData, focusedOn))
                {
                    asyncTask = command.RunCommand();
                    return command.PreventDefault;
                }
            }

            return false;
        }

        private void GoNextTab(bool forward)
        {
            var idx = this._statuses.SelectedTabIndex;
            var tabCount = this._statuses.Tabs.Count;
            if (forward)
            {
                idx += 1;
                if (idx > tabCount - 1) idx = 0;
            }
            else
            {
                idx -= 1;
                if (idx < 0) idx = tabCount - 1;
            }
            ListTab.SelectedIndex = idx;
        }

        private void CopyStot()
        {
            var sb = new StringBuilder();
            var tab = this.CurrentTab;
            var IsProtected = false;
            var isDm = tab.TabType == MyCommon.TabUsageType.DirectMessage;
            foreach (var post in tab.SelectedPosts)
            {
                if (post.IsDeleted) continue;
                if (!isDm)
                {
                    if (post.RetweetedId != null)
                        sb.AppendFormat("{0}:{1} [https://twitter.com/{0}/status/{2}]{3}", post.ScreenName, post.TextSingleLine, post.RetweetedId, Environment.NewLine);
                    else
                        sb.AppendFormat("{0}:{1} [https://twitter.com/{0}/status/{2}]{3}", post.ScreenName, post.TextSingleLine, post.StatusId, Environment.NewLine);
                }
                else
                {
                    sb.AppendFormat("{0}:{1} [{2}]{3}", post.ScreenName, post.TextSingleLine, post.StatusId, Environment.NewLine);
                }
            }
            if (IsProtected)
            {
                MessageBox.Show(Properties.Resources.CopyStotText1);
            }
            if (sb.Length > 0)
            {
                var clstr = sb.ToString();
                try
                {
                    Clipboard.SetDataObject(clstr, false, 5, 100);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void CopyIdUri()
        {
            var tab = this.CurrentTab;
            if (tab == null || tab is DirectMessagesTabModel)
                return;

            var copyUrls = new List<string>();
            foreach (var post in tab.SelectedPosts)
                copyUrls.Add(MyCommon.GetStatusUrl(post));

            if (copyUrls.Count == 0)
                return;

            try
            {
                Clipboard.SetDataObject(string.Join(Environment.NewLine, copyUrls), false, 5, 100);
            }
            catch (ExternalException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void GoFav(bool forward)
        {
            var tab = this.CurrentTab;
            if (tab.AllCount == 0)
                return;

            var selectedIndex = tab.SelectedIndex;

            int fIdx;
            int toIdx;
            int stp;

            if (forward)
            {
                if (selectedIndex == -1)
                {
                    fIdx = 0;
                }
                else
                {
                    fIdx = selectedIndex + 1;
                    if (fIdx > tab.AllCount - 1)
                        return;
                }
                toIdx = tab.AllCount;
                stp = 1;
            }
            else
            {
                if (selectedIndex == -1)
                {
                    fIdx = tab.AllCount - 1;
                }
                else
                {
                    fIdx = selectedIndex - 1;
                    if (fIdx < 0)
                        return;
                }
                toIdx = -1;
                stp = -1;
            }

            for (var idx = fIdx; idx != toIdx; idx += stp)
            {
                if (tab[idx].IsFav)
                {
                    var listView = this.CurrentListView;
                    SelectListItem(listView, idx);
                    listView.EnsureVisible(idx);
                    break;
                }
            }
        }

        private void GoSamePostToAnotherTab(bool left)
        {
            var tab = this.CurrentTab;

            // Directタブは対象外（見つかるはずがない）
            if (tab.TabType == MyCommon.TabUsageType.DirectMessage)
                return;

            var selectedStatusId = tab.SelectedStatusId;
            if (selectedStatusId == -1)
                return;

            int fIdx, toIdx, stp;

            if (left)
            {
                // 左のタブへ
                if (ListTab.SelectedIndex == 0)
                {
                    return;
                }
                else
                {
                    fIdx = ListTab.SelectedIndex - 1;
                }
                toIdx = -1;
                stp = -1;
            }
            else
            {
                // 右のタブへ
                if (ListTab.SelectedIndex == ListTab.TabCount - 1)
                {
                    return;
                }
                else
                {
                    fIdx = ListTab.SelectedIndex + 1;
                }
                toIdx = ListTab.TabCount;
                stp = 1;
            }

            for (var tabidx = fIdx; tabidx != toIdx; tabidx += stp)
            {
                var targetTab = this._statuses.Tabs[tabidx];

                // Directタブは対象外
                if (targetTab.TabType == MyCommon.TabUsageType.DirectMessage)
                    continue;

                var foundIndex = targetTab.IndexOf(selectedStatusId);
                if (foundIndex != -1)
                {
                    ListTab.SelectedIndex = tabidx;
                    var listView = this.CurrentListView;
                    SelectListItem(listView, foundIndex);
                    listView.EnsureVisible(foundIndex);
                    return;
                }
            }
        }

        private void GoPost(bool forward)
        {
            var tab = this.CurrentTab;
            var currentPost = this.CurrentPost;

            if (currentPost == null)
                return;

            var selectedIndex = tab.SelectedIndex;

            int fIdx, toIdx, stp;

            if (forward)
            {
                fIdx = selectedIndex + 1;
                if (fIdx > tab.AllCount - 1) return;
                toIdx = tab.AllCount;
                stp = 1;
            }
            else
            {
                fIdx = selectedIndex - 1;
                if (fIdx < 0) return;
                toIdx = -1;
                stp = -1;
            }

            string name;
            if (currentPost.RetweetedBy == null)
            {
                name = currentPost.ScreenName;
            }
            else
            {
                name = currentPost.RetweetedBy;
            }
            for (var idx = fIdx; idx != toIdx; idx += stp)
            {
                var post = tab[idx];
                if (post.RetweetedId == null)
                {
                    if (post.ScreenName == name)
                    {
                        var listView = this.CurrentListView;
                        SelectListItem(listView, idx);
                        listView.EnsureVisible(idx);
                        break;
                    }
                }
                else
                {
                    if (post.RetweetedBy == name)
                    {
                        var listView = this.CurrentListView;
                        SelectListItem(listView, idx);
                        listView.EnsureVisible(idx);
                        break;
                    }
                }
            }
        }

        private void GoRelPost(bool forward)
        {
            var tab = this.CurrentTab;
            var selectedIndex = tab.SelectedIndex;

            if (selectedIndex == -1)
                return;

            int fIdx, toIdx, stp;

            if (forward)
            {
                fIdx = selectedIndex + 1;
                if (fIdx > tab.AllCount - 1) return;
                toIdx = tab.AllCount;
                stp = 1;
            }
            else
            {
                fIdx = selectedIndex - 1;
                if (fIdx < 0) return;
                toIdx = -1;
                stp = -1;
            }

            if (!_anchorFlag)
            {
                var currentPost = this.CurrentPost;
                if (currentPost == null) return;
                _anchorPost = currentPost;
                _anchorFlag = true;
            }
            else
            {
                if (_anchorPost == null) return;
            }

            for (var idx = fIdx; idx != toIdx; idx += stp)
            {
                var post = tab[idx];
                if (post.ScreenName == _anchorPost.ScreenName ||
                    post.RetweetedBy == _anchorPost.ScreenName ||
                    post.ScreenName == _anchorPost.RetweetedBy ||
                    (!MyCommon.IsNullOrEmpty(post.RetweetedBy) && post.RetweetedBy == _anchorPost.RetweetedBy) ||
                    _anchorPost.ReplyToList.Any(x => x.UserId == post.UserId) ||
                    _anchorPost.ReplyToList.Any(x => x.UserId == post.RetweetedByUserId) ||
                    post.ReplyToList.Any(x => x.UserId == _anchorPost.UserId) ||
                    post.ReplyToList.Any(x => x.UserId == _anchorPost.RetweetedByUserId))
                {
                    var listView = this.CurrentListView;
                    SelectListItem(listView, idx);
                    listView.EnsureVisible(idx);
                    break;
                }
            }
        }

        private void GoAnchor()
        {
            if (_anchorPost == null) return;
            var idx = this.CurrentTab.IndexOf(_anchorPost.StatusId);
            if (idx == -1) return;

            var listView = this.CurrentListView;
            SelectListItem(listView, idx);
            listView.EnsureVisible(idx);
        }

        private void GoTopEnd(bool GoTop)
        {
            var listView = this.CurrentListView;
            if (listView.VirtualListSize == 0)
                return;

            ListViewItem _item;
            int idx;

            if (GoTop)
            {
                _item = listView.GetItemAt(0, 25);
                if (_item == null)
                    idx = 0;
                else
                    idx = _item.Index;
            }
            else
            {
                _item = listView.GetItemAt(0, listView.ClientSize.Height - 1);
                if (_item == null)
                    idx = listView.VirtualListSize - 1;
                else
                    idx = _item.Index;
            }
            SelectListItem(listView, idx);
        }

        private void GoMiddle()
        {
            var listView = this.CurrentListView;
            if (listView.VirtualListSize == 0)
                return;

            ListViewItem _item;
            int idx1;
            int idx2;
            int idx3;

            _item = listView.GetItemAt(0, 0);
            if (_item == null)
            {
                idx1 = 0;
            }
            else
            {
                idx1 = _item.Index;
            }

            _item = listView.GetItemAt(0, listView.ClientSize.Height - 1);
            if (_item == null)
            {
                idx2 = listView.VirtualListSize - 1;
            }
            else
            {
                idx2 = _item.Index;
            }
            idx3 = (idx1 + idx2) / 2;

            SelectListItem(listView, idx3);
        }

        private void GoLast()
        {
            var listView = this.CurrentListView;
            if (listView.VirtualListSize == 0) return;

            if (_statuses.SortOrder == SortOrder.Ascending)
            {
                SelectListItem(listView, listView.VirtualListSize - 1);
                listView.EnsureVisible(listView.VirtualListSize - 1);
            }
            else
            {
                SelectListItem(listView, 0);
                listView.EnsureVisible(0);
            }
        }

        private void MoveTop()
        {
            var listView = this.CurrentListView;
            if (listView.SelectedIndices.Count == 0) return;
            var idx = listView.SelectedIndices[0];
            if (_statuses.SortOrder == SortOrder.Ascending)
            {
                listView.EnsureVisible(listView.VirtualListSize - 1);
            }
            else
            {
                listView.EnsureVisible(0);
            }
            listView.EnsureVisible(idx);
        }

        private async Task GoInReplyToPostTree()
        {
            var curTabClass = this.CurrentTab;
            var currentPost = this.CurrentPost;

            if (currentPost == null)
                return;

            if (curTabClass.TabType == MyCommon.TabUsageType.PublicSearch && currentPost.InReplyToStatusId == null && currentPost.TextFromApi.Contains("@"))
            {
                try
                {
                    var post = await tw.GetStatusApi(false, currentPost.StatusId);

                    currentPost.InReplyToStatusId = post.InReplyToStatusId;
                    currentPost.InReplyToUser = post.InReplyToUser;
                    currentPost.IsReply = post.IsReply;
                    this.PurgeListViewItemCache();

                    var index = curTabClass.SelectedIndex;
                    this.CurrentListView.RedrawItems(index, index, false);
                }
                catch (WebApiException ex)
                {
                    this.StatusLabel.Text = $"Err:{ex.Message}(GetStatus)";
                }
            }

            if (!(this.ExistCurrentPost && currentPost.InReplyToUser != null && currentPost.InReplyToStatusId != null)) return;

            if (replyChains == null || (replyChains.Count > 0 && replyChains.Peek().InReplyToId != currentPost.StatusId))
            {
                replyChains = new Stack<ReplyChain>();
            }
            replyChains.Push(new ReplyChain(currentPost.StatusId, currentPost.InReplyToStatusId.Value, curTabClass));

            int inReplyToIndex;
            string inReplyToTabName;
            var inReplyToId = currentPost.InReplyToStatusId.Value;
            var inReplyToUser = currentPost.InReplyToUser;

            var inReplyToPosts = from tab in _statuses.Tabs
                                 orderby tab != curTabClass
                                 from post in tab.Posts.Values
                                 where post.StatusId == inReplyToId
                                 let index = tab.IndexOf(post.StatusId)
                                 where index != -1
                                 select new {Tab = tab, Index = index};

            var inReplyPost = inReplyToPosts.FirstOrDefault();
            if (inReplyPost == null)
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        var post = await tw.GetStatusApi(false, currentPost.InReplyToStatusId.Value)
                            .ConfigureAwait(false);
                        post.IsRead = true;

                        _statuses.AddPost(post);
                        _statuses.DistributePosts();
                    });
                }
                catch (WebApiException ex)
                {
                    this.StatusLabel.Text = $"Err:{ex.Message}(GetStatus)";
                    await this.OpenUriInBrowserAsync(MyCommon.GetStatusUrl(inReplyToUser, inReplyToId));
                    return;
                }

                this.RefreshTimeline();

                inReplyPost = inReplyToPosts.FirstOrDefault();
                if (inReplyPost == null)
                {
                    await this.OpenUriInBrowserAsync(MyCommon.GetStatusUrl(inReplyToUser, inReplyToId));
                    return;
                }
            }
            inReplyToTabName = inReplyPost.Tab.TabName;
            inReplyToIndex = inReplyPost.Index;

            var tabIndex = this._statuses.Tabs.IndexOf(inReplyToTabName);
            var tabPage = this.ListTab.TabPages[tabIndex];
            var listView = (DetailsListView)tabPage.Tag;

            if (this.CurrentTabName != inReplyToTabName)
            {
                this.ListTab.SelectedIndex = tabIndex;
            }

            this.SelectListItem(listView, inReplyToIndex);
            listView.EnsureVisible(inReplyToIndex);
        }

        private void GoBackInReplyToPostTree(bool parallel = false, bool isForward = true)
        {
            var curTabClass = this.CurrentTab;
            var currentPost = this.CurrentPost;

            if (currentPost == null)
                return;

            if (parallel)
            {
                if (currentPost.InReplyToStatusId != null)
                {
                    var posts = from t in _statuses.Tabs
                                from p in t.Posts
                                where p.Value.StatusId != currentPost.StatusId && p.Value.InReplyToStatusId == currentPost.InReplyToStatusId
                                let indexOf = t.IndexOf(p.Value.StatusId)
                                where indexOf > -1
                                orderby isForward ? indexOf : indexOf * -1
                                orderby t != curTabClass
                                select new {Tab = t, Post = p.Value, Index = indexOf};
                    try
                    {
                        var postList = posts.ToList();
                        for (var i = postList.Count - 1; i >= 0; i--)
                        {
                            var index = i;
                            if (postList.FindIndex(pst => pst.Post.StatusId == postList[index].Post.StatusId) != index)
                            {
                                postList.RemoveAt(index);
                            }
                        }
                        var currentIndex = this.CurrentTab.SelectedIndex;
                        var post = postList.FirstOrDefault(pst => pst.Tab == curTabClass && isForward ? pst.Index > currentIndex : pst.Index < currentIndex);
                        if (post == null) post = postList.FirstOrDefault(pst => pst.Tab != curTabClass);
                        if (post == null) post = postList.First();
                        var tabIndex = this._statuses.Tabs.IndexOf(post.Tab);
                        this.ListTab.SelectedIndex = tabIndex;
                        var listView = this.CurrentListView;
                        SelectListItem(listView, post.Index);
                        listView.EnsureVisible(post.Index);
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                }
            }
            else
            {
                if (replyChains == null || replyChains.Count < 1)
                {
                    var posts = from t in _statuses.Tabs
                                from p in t.Posts
                                where p.Value.InReplyToStatusId == currentPost.StatusId
                                let indexOf = t.IndexOf(p.Value.StatusId)
                                where indexOf > -1
                                orderby indexOf
                                orderby t != curTabClass
                                select new {Tab = t, Index = indexOf};
                    try
                    {
                        var post = posts.First();
                        var tabIndex = this._statuses.Tabs.IndexOf(post.Tab);
                        this.ListTab.SelectedIndex = tabIndex;
                        var listView = this.CurrentListView;
                        SelectListItem(listView, post.Index);
                        listView.EnsureVisible(post.Index);
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                }
                else
                {
                    var chainHead = replyChains.Pop();
                    if (chainHead.InReplyToId == currentPost.StatusId)
                    {
                        var tab = chainHead.OriginalTab;
                        if (!this._statuses.Tabs.Contains(tab))
                        {
                            replyChains = null;
                        }
                        else
                        {
                            var idx = tab.IndexOf(chainHead.OriginalId);
                            if (idx == -1)
                            {
                                replyChains = null;
                            }
                            else
                            {
                                var tabIndex = this._statuses.Tabs.IndexOf(tab);
                                try
                                {
                                    this.ListTab.SelectedIndex = tabIndex;
                                }
                                catch (Exception)
                                {
                                    replyChains = null;
                                }
                                var listView = this.CurrentListView;
                                SelectListItem(listView, idx);
                                listView.EnsureVisible(idx);
                            }
                        }
                    }
                    else
                    {
                        replyChains = null;
                        this.GoBackInReplyToPostTree(parallel);
                    }
                }
            }
        }

        private void GoBackSelectPostChain()
        {
            if (this.selectPostChains.Count > 1)
            {
                var idx = -1;
                TabModel? foundTab = null;

                do
                {
                    try
                    {
                        this.selectPostChains.Pop();
                        var (tab, post) = this.selectPostChains.Peek();

                        if (!this._statuses.Tabs.Contains(tab))
                            continue; // 該当タブが存在しないので無視

                        if (post != null)
                        {
                            idx = tab.IndexOf(post.StatusId);
                            if (idx == -1) continue;  //該当ポストが存在しないので無視
                        }

                        foundTab = tab;

                        this.selectPostChains.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    break;
                }
                while (this.selectPostChains.Count > 1);

                if (foundTab == null)
                {
                    //状態がおかしいので処理を中断
                    //履歴が残り1つであればクリアしておく
                    if (this.selectPostChains.Count == 1)
                        this.selectPostChains.Clear();
                    return;
                }

                var tabIndex = this._statuses.Tabs.IndexOf(foundTab);
                var tabPage = this.ListTab.TabPages[tabIndex];
                var lst = (DetailsListView)tabPage.Tag;
                this.ListTab.SelectedIndex = tabIndex;

                if (idx > -1)
                {
                    SelectListItem(lst, idx);
                    lst.EnsureVisible(idx);
                }
                lst.Focus();
            }
        }

        private void PushSelectPostChain()
        {
            var currentTab = this.CurrentTab;
            var currentPost = this.CurrentPost;

            var count = this.selectPostChains.Count;
            if (count > 0)
            {
                var (tab, post) = this.selectPostChains.Peek();
                if (tab == currentTab)
                {
                    if (post == currentPost) return;  //最新の履歴と同一
                    if (post == null) this.selectPostChains.Pop();  //置き換えるため削除
                }
            }
            if (count >= 2500) TrimPostChain();
            this.selectPostChains.Push((currentTab, currentPost));
        }

        private void TrimPostChain()
        {
            if (this.selectPostChains.Count <= 2000) return;
            var p = new Stack<(TabModel, PostClass?)>(2000);
            for (var i = 0; i < 2000; i++)
            {
                p.Push(this.selectPostChains.Pop());
            }
            this.selectPostChains.Clear();
            for (var i = 0; i < 2000; i++)
            {
                this.selectPostChains.Push(p.Pop());
            }
        }

        private bool GoStatus(long statusId)
        {
            if (statusId == 0) return false;

            var tab = this._statuses.Tabs
                .Where(x => x.TabType != MyCommon.TabUsageType.DirectMessage)
                .Where(x => x.Contains(statusId))
                .FirstOrDefault();

            if (tab == null)
                return false;

            var index = tab.IndexOf(statusId);

            var tabIndex = this._statuses.Tabs.IndexOf(tab);
            this.ListTab.SelectedIndex = tabIndex;

            var listView = this.CurrentListView;
            this.SelectListItem(listView, index);
            listView.EnsureVisible(index);

            return true;
        }

        private bool GoDirectMessage(long statusId)
        {
            if (statusId == 0) return false;

            var tab = this._statuses.DirectMessageTab;
            var index = tab.IndexOf(statusId);

            if (index == -1)
                return false;

            var tabIndex = this._statuses.Tabs.IndexOf(tab);
            this.ListTab.SelectedIndex = tabIndex;

            var listView = this.CurrentListView;
            this.SelectListItem(listView, index);
            listView.EnsureVisible(index);

            return true;
        }

        private void MyList_MouseClick(object sender, MouseEventArgs e)
            => this._anchorFlag = false;

        private void StatusText_Enter(object sender, EventArgs e)
        {
            // フォーカスの戻り先を StatusText に設定
            this.Tag = StatusText;
            StatusText.BackColor = _clInputBackcolor;
        }

        public Color InputBackColor
        {
            get => _clInputBackcolor;
            set => _clInputBackcolor = value;
        }

        private void StatusText_Leave(object sender, EventArgs e)
        {
            // フォーカスがメニューに遷移しないならばフォーカスはタブに移ることを期待
            if (ListTab.SelectedTab != null && MenuStrip1.Tag == null) this.Tag = ListTab.SelectedTab.Tag;
            StatusText.BackColor = Color.FromKnownColor(KnownColor.Window);
        }

        private async void StatusText_KeyDown(object sender, KeyEventArgs e)
        {
            if (CommonKeyDown(e.KeyData, FocusedControl.StatusText, out var asyncTask))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);

            if (asyncTask != null)
                await asyncTask;
        }

        private void SaveConfigsAll(bool ifModified)
        {
            if (!ifModified)
            {
                SaveConfigsCommon();
                SaveConfigsLocal();
                SaveConfigsTabs();
                SaveConfigsAtId();
            }
            else
            {
                if (ModifySettingCommon) SaveConfigsCommon();
                if (ModifySettingLocal) SaveConfigsLocal();
                if (ModifySettingAtId) SaveConfigsAtId();
            }
        }

        private void SaveConfigsAtId()
        {
            if (_ignoreConfigSave || !SettingManager.Common.UseAtIdSupplement && AtIdSupl == null) return;

            ModifySettingAtId = false;
            SettingManager.AtIdList.AtIdList = this.AtIdSupl.GetItemList();
            SettingManager.SaveAtIdList();
        }

        private void SaveConfigsCommon()
        {
            if (_ignoreConfigSave) return;

            ModifySettingCommon = false;
            lock (_syncObject)
            {
                SettingManager.Common.UserName = tw.Username;
                SettingManager.Common.UserId = tw.UserId;
                SettingManager.Common.Token = tw.AccessToken;
                SettingManager.Common.TokenSecret = tw.AccessTokenSecret;
                SettingManager.Common.SortOrder = (int)_statuses.SortOrder;
                SettingManager.Common.SortColumn = this._statuses.SortMode switch
                {
                    ComparerMode.Nickname => 1, // ニックネーム
                    ComparerMode.Data => 2, // 本文
                    ComparerMode.Id => 3, // 時刻=発言Id
                    ComparerMode.Name => 4, // 名前
                    ComparerMode.Source => 7, // Source
                    _ => throw new InvalidOperationException($"Invalid sort mode: {this._statuses.SortMode}"),
                };
                SettingManager.Common.HashTags = HashMgr.HashHistories;
                if (HashMgr.IsPermanent)
                {
                    SettingManager.Common.HashSelected = HashMgr.UseHash;
                }
                else
                {
                    SettingManager.Common.HashSelected = "";
                }
                SettingManager.Common.HashIsHead = HashMgr.IsHead;
                SettingManager.Common.HashIsPermanent = HashMgr.IsPermanent;
                SettingManager.Common.HashIsNotAddToAtReply = HashMgr.IsNotAddToAtReply;
                SettingManager.Common.TrackWord = tw.TrackWord;
                SettingManager.Common.AllAtReply = tw.AllAtReply;
                SettingManager.Common.UseImageService = ImageSelector.ServiceIndex;
                SettingManager.Common.UseImageServiceName = ImageSelector.ServiceName;

                SettingManager.SaveCommon();
            }
        }

        private void SaveConfigsLocal()
        {
            if (_ignoreConfigSave) return;
            lock (_syncObject)
            {
                ModifySettingLocal = false;
                SettingManager.Local.ScaleDimension = this.CurrentAutoScaleDimensions;
                SettingManager.Local.FormSize = _mySize;
                SettingManager.Local.FormLocation = _myLoc;
                SettingManager.Local.SplitterDistance = _mySpDis;
                SettingManager.Local.PreviewDistance = _mySpDis3;
                SettingManager.Local.StatusMultiline = StatusText.Multiline;
                SettingManager.Local.StatusTextHeight = _mySpDis2;

                SettingManager.Local.FontUnread = _fntUnread;
                SettingManager.Local.ColorUnread = _clUnread;
                SettingManager.Local.FontRead = _fntReaded;
                SettingManager.Local.ColorRead = _clReaded;
                SettingManager.Local.FontDetail = _fntDetail;
                SettingManager.Local.ColorDetail = _clDetail;
                SettingManager.Local.ColorDetailBackcolor = _clDetailBackcolor;
                SettingManager.Local.ColorDetailLink = _clDetailLink;
                SettingManager.Local.ColorFav = _clFav;
                SettingManager.Local.ColorOWL = _clOWL;
                SettingManager.Local.ColorRetweet = _clRetweet;
                SettingManager.Local.ColorSelf = _clSelf;
                SettingManager.Local.ColorAtSelf = _clAtSelf;
                SettingManager.Local.ColorTarget = _clTarget;
                SettingManager.Local.ColorAtTarget = _clAtTarget;
                SettingManager.Local.ColorAtFromTarget = _clAtFromTarget;
                SettingManager.Local.ColorAtTo = _clAtTo;
                SettingManager.Local.ColorListBackcolor = _clListBackcolor;
                SettingManager.Local.ColorInputBackcolor = _clInputBackcolor;
                SettingManager.Local.ColorInputFont = _clInputFont;
                SettingManager.Local.FontInputFont = _fntInputFont;

                if (_ignoreConfigSave) return;
                SettingManager.SaveLocal();
            }
        }

        private void SaveConfigsTabs()
        {
            var tabSettingList = new List<SettingTabs.SettingTabItem>();

            var tabs = this._statuses.Tabs.Append(this._statuses.MuteTab);

            foreach (var tab in tabs)
            {
                if (!tab.IsPermanentTabType)
                    continue;

                var tabSetting = new SettingTabs.SettingTabItem
                {
                    TabName = tab.TabName,
                    TabType = tab.TabType,
                    UnreadManage = tab.UnreadManage,
                    Protected = tab.Protected,
                    Notify = tab.Notify,
                    SoundFile = tab.SoundFile,
                };

                switch (tab)
                {
                    case FilterTabModel filterTab:
                        tabSetting.FilterArray = filterTab.FilterArray;
                        break;
                    case UserTimelineTabModel userTab:
                        tabSetting.User = userTab.ScreenName;
                        break;
                    case PublicSearchTabModel searchTab:
                        tabSetting.SearchWords = searchTab.SearchWords;
                        tabSetting.SearchLang = searchTab.SearchLang;
                        break;
                    case ListTimelineTabModel listTab:
                        tabSetting.ListInfo = listTab.ListInfo;
                        break;
                }

                tabSettingList.Add(tabSetting);
            }

            SettingManager.Tabs.Tabs = tabSettingList;
            SettingManager.SaveTabs();
        }

        private async void OpenURLFileMenuItem_Click(object sender, EventArgs e)
        {
            var ret = InputDialog.Show(this, Properties.Resources.OpenURL_InputText, Properties.Resources.OpenURL_Caption, out var inputText);
            if (ret != DialogResult.OK)
                return;

            var match = Twitter.StatusUrlRegex.Match(inputText);
            if (!match.Success)
            {
                MessageBox.Show(this, Properties.Resources.OpenURL_InvalidFormat,
                    Properties.Resources.OpenURL_Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var statusId = long.Parse(match.Groups["StatusId"].Value);
                await this.OpenRelatedTab(statusId);
            }
            catch (TabException ex)
            {
                MessageBox.Show(this, ex.Message, ApplicationSettings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveLogMenuItem_Click(object sender, EventArgs e)
        {
            var tab = this.CurrentTab;

            var rslt = MessageBox.Show(string.Format(Properties.Resources.SaveLogMenuItem_ClickText1, Environment.NewLine),
                    Properties.Resources.SaveLogMenuItem_ClickText2,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (rslt == DialogResult.Cancel) return;

            SaveFileDialog1.FileName = $"{ApplicationSettings.AssemblyName}Posts{DateTimeUtc.Now.ToLocalTime():yyMMdd-HHmmss}.tsv";
            SaveFileDialog1.InitialDirectory = Application.ExecutablePath;
            SaveFileDialog1.Filter = Properties.Resources.SaveLogMenuItem_ClickText3;
            SaveFileDialog1.FilterIndex = 0;
            SaveFileDialog1.Title = Properties.Resources.SaveLogMenuItem_ClickText4;
            SaveFileDialog1.RestoreDirectory = true;

            if (SaveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!SaveFileDialog1.ValidateNames) return;
                using var sw = new StreamWriter(SaveFileDialog1.FileName, false, Encoding.UTF8);
                if (rslt == DialogResult.Yes)
                {
                    //All
                    for (var idx = 0; idx < tab.AllCount; idx++)
                    {
                        var post = tab[idx];
                        var protect = "";
                        if (post.IsProtect)
                            protect = "Protect";
                        sw.WriteLine(post.Nickname + "\t" +
                                 "\"" + post.TextFromApi.Replace("\n", "").Replace("\"", "\"\"") + "\"" + "\t" +
                                 post.CreatedAt.ToLocalTimeString() + "\t" +
                                 post.ScreenName + "\t" +
                                 post.StatusId + "\t" +
                                 post.ImageUrl + "\t" +
                                 "\"" + post.Text.Replace("\n", "").Replace("\"", "\"\"") + "\"" + "\t" +
                                 protect);
                    }
                }
                else
                {
                    foreach (var post in this.CurrentTab.SelectedPosts)
                    {
                        var protect = "";
                        if (post.IsProtect)
                            protect = "Protect";
                        sw.WriteLine(post.Nickname + "\t" +
                                 "\"" + post.TextFromApi.Replace("\n", "").Replace("\"", "\"\"") + "\"" + "\t" +
                                 post.CreatedAt.ToLocalTimeString() + "\t" +
                                 post.ScreenName + "\t" +
                                 post.StatusId + "\t" +
                                 post.ImageUrl + "\t" +
                                 "\"" + post.Text.Replace("\n", "").Replace("\"", "\"\"") + "\"" + "\t" +
                                 protect);
                    }
                }
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
        }

        public bool TabRename(string origTabName, [NotNullWhen(true)] out string? newTabName)
        {
            //タブ名変更
            newTabName = null;
            using (var inputName = new InputTabName())
            {
                inputName.TabName = origTabName;
                inputName.ShowDialog();
                if (inputName.DialogResult == DialogResult.Cancel) return false;
                newTabName = inputName.TabName;
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
            if (!MyCommon.IsNullOrEmpty(newTabName))
            {
                //新タブ名存在チェック
                if (this._statuses.ContainsTab(newTabName))
                {
                    var tmp = string.Format(Properties.Resources.Tabs_DoubleClickText1, newTabName);
                    MessageBox.Show(tmp, Properties.Resources.Tabs_DoubleClickText2, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return false;
                }

                var tabIndex = this._statuses.Tabs.IndexOf(origTabName);
                var tabPage = this.ListTab.TabPages[tabIndex];

                // タブ名を変更
                if (tabPage != null)
                    tabPage.Text = newTabName;

                _statuses.RenameTab(origTabName, newTabName);

                SaveConfigsCommon();
                SaveConfigsTabs();
                _rclickTabName = newTabName;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ListTab_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
                {
                    if (this.ListTab.GetTabRect(index).Contains(e.Location))
                    {
                        this.RemoveSpecifiedTab(tab.TabName, true);
                        this.SaveConfigsTabs();
                        break;
                    }
                }
            }
        }

        private void ListTab_DoubleClick(object sender, MouseEventArgs e)
            => this.TabRename(this.CurrentTabName, out _);

        private void ListTab_MouseDown(object sender, MouseEventArgs e)
        {
            if (SettingManager.Common.TabMouseLock) return;
            if (e.Button == MouseButtons.Left)
            {
                foreach (var i in Enumerable.Range(0, this._statuses.Tabs.Count))
                {
                    if (this.ListTab.GetTabRect(i).Contains(e.Location))
                    {
                        _tabDrag = true;
                        _tabMouseDownPoint = e.Location;
                        break;
                    }
                }
            }
            else
            {
                _tabDrag = false;
            }
        }

        private void ListTab_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TabPage)))
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private void ListTab_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TabPage))) return;

            _tabDrag = false;
            var tn = "";
            var bef = false;
            var cpos = new Point(e.X, e.Y);
            var spos = ListTab.PointToClient(cpos);
            foreach (var (tab, index) in this._statuses.Tabs.WithIndex())
            {
                var rect = ListTab.GetTabRect(index);
                if (rect.Contains(spos))
                {
                    tn = tab.TabName;
                    if (spos.X <= (rect.Left + rect.Right) / 2)
                        bef = true;
                    else
                        bef = false;

                    break;
                }
            }

            //タブのないところにドロップ->最後尾へ移動
            if (MyCommon.IsNullOrEmpty(tn))
            {
                var lastTab = this._statuses.Tabs.Last();
                tn = lastTab.TabName;
                bef = false;
            }

            var tp = (TabPage)e.Data.GetData(typeof(TabPage));
            if (tp.Text == tn) return;

            ReOrderTab(tp.Text, tn, bef);
        }

        public void ReOrderTab(string targetTabText, string baseTabText, bool isBeforeBaseTab)
        {
            var baseIndex = this.GetTabPageIndex(baseTabText);
            if (baseIndex == -1)
                return;

            var targetIndex = this.GetTabPageIndex(targetTabText);
            if (targetIndex == -1)
                return;

            using (ControlTransaction.Layout(this.ListTab))
            {
                var tab = this._statuses.Tabs[targetIndex];
                var tabPage = this.ListTab.TabPages[targetIndex];

                this.ListTab.TabPages.Remove(tabPage);

                if (targetIndex < baseIndex)
                    baseIndex--;

                if (!isBeforeBaseTab)
                    baseIndex++;

                this._statuses.MoveTab(baseIndex, tab);

                ListTab.TabPages.Insert(baseIndex, tabPage);
            }

            SaveConfigsTabs();
        }

        private void MakeReplyOrDirectStatus(bool isAuto = true, bool isReply = true, bool isAll = false)
        {
            //isAuto:true=先頭に挿入、false=カーソル位置に挿入
            //isReply:true=@,false=DM
            if (!StatusText.Enabled) return;
            if (!this.ExistCurrentPost) return;

            var tab = this.CurrentTab;
            var selectedPosts = tab.SelectedPosts;

            // 複数あてリプライはReplyではなく通常ポスト
            //↑仕様変更で全部リプライ扱いでＯＫ（先頭ドット付加しない）
            //090403暫定でドットを付加しないようにだけ修正。単独と複数の処理は統合できると思われる。
            //090513 all @ replies 廃止の仕様変更によりドット付加に戻し(syo68k)

            if (selectedPosts.Length > 0)
            {
                // アイテムが1件以上選択されている
                if (selectedPosts.Length == 1 && !isAll && this.ExistCurrentPost)
                {
                    var post = selectedPosts.Single();

                    // 単独ユーザー宛リプライまたはDM
                    if ((tab.TabType == MyCommon.TabUsageType.DirectMessage && isAuto) || (!isAuto && !isReply))
                    {
                        // ダイレクトメッセージ
                        this.inReplyTo = null;
                        StatusText.Text = "D " + post.ScreenName + " " + StatusText.Text;
                        StatusText.SelectionStart = StatusText.Text.Length;
                        StatusText.Focus();
                        return;
                    }
                    if (MyCommon.IsNullOrEmpty(StatusText.Text))
                    {
                        //空の場合
                        var inReplyToStatusId = post.RetweetedId ?? post.StatusId;
                        var inReplyToScreenName = post.ScreenName;
                        this.inReplyTo = (inReplyToStatusId, inReplyToScreenName);

                        // ステータステキストが入力されていない場合先頭に@ユーザー名を追加する
                        StatusText.Text = "@" + post.ScreenName + " ";
                    }
                    else
                    {
                        //何か入力済の場合

                        if (isAuto)
                        {
                            //1件選んでEnter or DoubleClick
                            if (StatusText.Text.Contains("@" + post.ScreenName + " "))
                            {
                                if (this.inReplyTo?.ScreenName == post.ScreenName)
                                {
                                    //返信先書き換え
                                    var inReplyToStatusId = post.RetweetedId ?? post.StatusId;
                                    var inReplyToScreenName = post.ScreenName;
                                    this.inReplyTo = (inReplyToStatusId, inReplyToScreenName);
                                }
                                return;
                            }
                            if (!StatusText.Text.StartsWith("@", StringComparison.Ordinal))
                            {
                                //文頭＠以外
                                if (StatusText.Text.StartsWith(". ", StringComparison.Ordinal))
                                {
                                    // 複数リプライ
                                    this.inReplyTo = null;
                                    StatusText.Text = StatusText.Text.Insert(2, "@" + post.ScreenName + " ");
                                }
                                else
                                {
                                    // 単独リプライ
                                    var inReplyToStatusId = post.RetweetedId ?? post.StatusId;
                                    var inReplyToScreenName = post.ScreenName;
                                    this.inReplyTo = (inReplyToStatusId, inReplyToScreenName);
                                    StatusText.Text = "@" + post.ScreenName + " " + StatusText.Text;
                                }
                            }
                            else
                            {
                                //文頭＠
                                // 複数リプライ
                                this.inReplyTo = null;
                                StatusText.Text = ". @" + post.ScreenName + " " + StatusText.Text;
                            }
                        }
                        else
                        {
                            //1件選んでCtrl-Rの場合（返信先操作せず）
                            var sidx = StatusText.SelectionStart;
                            var id = "@" + post.ScreenName + " ";
                            if (sidx > 0)
                            {
                                if (StatusText.Text.Substring(sidx - 1, 1) != " ")
                                {
                                    id = " " + id;
                                }
                            }
                            StatusText.Text = StatusText.Text.Insert(sidx, id);
                            sidx += id.Length;
                            StatusText.SelectionStart = sidx;
                            StatusText.Focus();
                            return;
                        }
                    }
                }
                else
                {
                    // 複数リプライ
                    if (!isAuto && !isReply) return;

                    //C-S-rか、複数の宛先を選択中にEnter/DoubleClick/C-r/C-S-r

                    if (isAuto)
                    {
                        //Enter or DoubleClick

                        var sTxt = StatusText.Text;
                        if (!sTxt.StartsWith(". ", StringComparison.Ordinal))
                        {
                            sTxt = ". " + sTxt;
                            this.inReplyTo = null;
                        }
                        foreach (var post in selectedPosts)
                        {
                            if (!sTxt.Contains("@" + post.ScreenName + " "))
                                sTxt = sTxt.Insert(2, "@" + post.ScreenName + " ");
                        }
                        StatusText.Text = sTxt;
                    }
                    else
                    {
                        //C-S-r or C-r

                        if (selectedPosts.Length > 1)
                        {
                            //複数ポスト選択

                            var ids = "";
                            var sidx = StatusText.SelectionStart;
                            foreach (var post in selectedPosts)
                            {
                                if (!ids.Contains("@" + post.ScreenName + " ") && post.UserId != tw.UserId)
                                {
                                    ids += "@" + post.ScreenName + " ";
                                }
                                if (isAll)
                                {
                                    foreach (var (_, screenName) in post.ReplyToList)
                                    {
                                        if (!ids.Contains("@" + screenName + " ") &&
                                            !screenName.Equals(tw.Username, StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var m = Regex.Match(post.TextFromApi, "[@＠](?<id>" + screenName + ")([^a-zA-Z0-9]|$)", RegexOptions.IgnoreCase);
                                            if (m.Success)
                                                ids += "@" + m.Result("${id}") + " ";
                                            else
                                                ids += "@" + screenName + " ";
                                        }
                                    }
                                }
                            }
                            if (ids.Length == 0) return;
                            if (!StatusText.Text.StartsWith(". ", StringComparison.Ordinal))
                            {
                                this.inReplyTo = null;
                                StatusText.Text = ". " + StatusText.Text;
                                sidx += 2;
                            }
                            if (sidx > 0)
                            {
                                if (StatusText.Text.Substring(sidx - 1, 1) != " ")
                                {
                                    ids = " " + ids;
                                }
                            }
                            StatusText.Text = StatusText.Text.Insert(sidx, ids);
                            sidx += ids.Length;
                            StatusText.SelectionStart = sidx;
                            StatusText.Focus();
                            return;
                        }
                        else
                        {
                            //1件のみ選択のC-S-r（返信元付加する可能性あり）

                            var ids = "";
                            var sidx = StatusText.SelectionStart;
                            var post = selectedPosts.Single();
                            if (!ids.Contains("@" + post.ScreenName + " ") && post.UserId != tw.UserId)
                            {
                                ids += "@" + post.ScreenName + " ";
                            }
                            foreach (var (_, screenName) in post.ReplyToList)
                            {
                                if (!ids.Contains("@" + screenName + " ") &&
                                    !screenName.Equals(tw.Username, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var m = Regex.Match(post.TextFromApi, "[@＠](?<id>" + screenName + ")([^a-zA-Z0-9]|$)", RegexOptions.IgnoreCase);
                                    if (m.Success)
                                        ids += "@" + m.Result("${id}") + " ";
                                    else
                                        ids += "@" + screenName + " ";
                                }
                            }
                            if (!MyCommon.IsNullOrEmpty(post.RetweetedBy))
                            {
                                if (!ids.Contains("@" + post.RetweetedBy + " ") && post.RetweetedByUserId != tw.UserId)
                                {
                                    ids += "@" + post.RetweetedBy + " ";
                                }
                            }
                            if (ids.Length == 0) return;
                            if (MyCommon.IsNullOrEmpty(StatusText.Text))
                            {
                                //未入力の場合のみ返信先付加
                                var inReplyToStatusId = post.RetweetedId ?? post.StatusId;
                                var inReplyToScreenName = post.ScreenName;
                                this.inReplyTo = (inReplyToStatusId, inReplyToScreenName);

                                StatusText.Text = ids;
                                StatusText.SelectionStart = ids.Length;
                                StatusText.Focus();
                                return;
                            }

                            if (sidx > 0)
                            {
                                if (StatusText.Text.Substring(sidx - 1, 1) != " ")
                                {
                                    ids = " " + ids;
                                }
                            }
                            StatusText.Text = StatusText.Text.Insert(sidx, ids);
                            sidx += ids.Length;
                            StatusText.SelectionStart = sidx;
                            StatusText.Focus();
                            return;
                        }
                    }
                }
                StatusText.SelectionStart = StatusText.Text.Length;
                StatusText.Focus();
            }
        }

        private void ListTab_MouseUp(object sender, MouseEventArgs e)
            => this._tabDrag = false;

        private int iconCnt = 0;
        private int blinkCnt = 0;
        private bool blink = false;

        private void RefreshTasktrayIcon()
        {
            void EnableTasktrayAnimation()
                => this.TimerRefreshIcon.Enabled = true;

            void DisableTasktrayAnimation()
                => this.TimerRefreshIcon.Enabled = false;

            var busyTasks = this.workerSemaphore.CurrentCount != MAX_WORKER_THREADS;
            if (busyTasks)
            {
                iconCnt += 1;
                if (iconCnt >= this.NIconRefresh.Length)
                    iconCnt = 0;

                NotifyIcon1.Icon = NIconRefresh[iconCnt];
                _myStatusError = false;
                EnableTasktrayAnimation();
                return;
            }

            var replyIconType = SettingManager.Common.ReplyIconState;
            var reply = false;
            if (replyIconType != MyCommon.REPLY_ICONSTATE.None)
            {
                var replyTab = this._statuses.GetTabByType<MentionsTabModel>();
                if (replyTab != null && replyTab.UnreadCount > 0)
                    reply = true;
            }

            if (replyIconType == MyCommon.REPLY_ICONSTATE.BlinkIcon && reply)
            {
                blinkCnt += 1;
                if (blinkCnt > 10)
                    blinkCnt = 0;

                if (blinkCnt == 0)
                    blink = !blink;

                NotifyIcon1.Icon = blink ? ReplyIconBlink : ReplyIcon;
                EnableTasktrayAnimation();
                return;
            }

            DisableTasktrayAnimation();

            iconCnt = 0;
            blinkCnt = 0;
            blink = false;

            // 優先度：リプライ→エラー→オフライン→アイドル
            // エラーは更新アイコンでクリアされる
            if (replyIconType == MyCommon.REPLY_ICONSTATE.StaticIcon && reply)
                NotifyIcon1.Icon = ReplyIcon;
            else if (_myStatusError)
                NotifyIcon1.Icon = NIconAtRed;
            else if (_myStatusOnline)
                NotifyIcon1.Icon = NIconAt;
            else
                NotifyIcon1.Icon = NIconAtSmoke;
        }

        private void TimerRefreshIcon_Tick(object sender, EventArgs e)
            => this.RefreshTasktrayIcon(); // 200ms

        private void ContextMenuTabProperty_Opening(object sender, CancelEventArgs e)
        {
            //右クリックの場合はタブ名が設定済。アプリケーションキーの場合は現在のタブを対象とする
            if (MyCommon.IsNullOrEmpty(_rclickTabName) || sender != ContextMenuTabProperty)
                _rclickTabName = this.CurrentTabName;

            if (_statuses == null) return;
            if (_statuses.Tabs == null) return;

            if (!this._statuses.Tabs.TryGetValue(this._rclickTabName, out var tb))
                return;

            NotifyDispMenuItem.Checked = tb.Notify;
            this.NotifyTbMenuItem.Checked = tb.Notify;

            soundfileListup = true;
            SoundFileComboBox.Items.Clear();
            this.SoundFileTbComboBox.Items.Clear();
            SoundFileComboBox.Items.Add("");
            this.SoundFileTbComboBox.Items.Add("");
            var oDir = new DirectoryInfo(Application.StartupPath + Path.DirectorySeparatorChar);
            if (Directory.Exists(Path.Combine(Application.StartupPath, "Sounds")))
            {
                oDir = oDir.GetDirectories("Sounds")[0];
            }
            foreach (var oFile in oDir.GetFiles("*.wav"))
            {
                SoundFileComboBox.Items.Add(oFile.Name);
                this.SoundFileTbComboBox.Items.Add(oFile.Name);
            }
            var idx = SoundFileComboBox.Items.IndexOf(tb.SoundFile);
            if (idx == -1) idx = 0;
            SoundFileComboBox.SelectedIndex = idx;
            this.SoundFileTbComboBox.SelectedIndex = idx;
            soundfileListup = false;
            UreadManageMenuItem.Checked = tb.UnreadManage;
            this.UnreadMngTbMenuItem.Checked = tb.UnreadManage;

            TabMenuControl(_rclickTabName);
        }

        private void TabMenuControl(string tabName)
        {
            var tabInfo = _statuses.GetTabByName(tabName)!;

            this.FilterEditMenuItem.Enabled = true;
            this.EditRuleTbMenuItem.Enabled = true;

            if (tabInfo.IsDefaultTabType)
            {
                this.ProtectTabMenuItem.Enabled = false;
                this.ProtectTbMenuItem.Enabled = false;
            }
            else
            {
                this.ProtectTabMenuItem.Enabled = true;
                this.ProtectTbMenuItem.Enabled = true;
            }

            if (tabInfo.IsDefaultTabType || tabInfo.Protected)
            {
                this.ProtectTabMenuItem.Checked = true;
                this.ProtectTbMenuItem.Checked = true;
                this.DeleteTabMenuItem.Enabled = false;
                this.DeleteTbMenuItem.Enabled = false;
            }
            else
            {
                this.ProtectTabMenuItem.Checked = false;
                this.ProtectTbMenuItem.Checked = false;
                this.DeleteTabMenuItem.Enabled = true;
                this.DeleteTbMenuItem.Enabled = true;
            }
        }

        private void ProtectTabMenuItem_Click(object sender, EventArgs e)
        {
            var checkState = ((ToolStripMenuItem)sender).Checked;

            // チェック状態を同期
            this.ProtectTbMenuItem.Checked = checkState;
            this.ProtectTabMenuItem.Checked = checkState;

            // ロック中はタブの削除を無効化
            this.DeleteTabMenuItem.Enabled = !checkState;
            this.DeleteTbMenuItem.Enabled = !checkState;

            if (MyCommon.IsNullOrEmpty(_rclickTabName)) return;
            _statuses.Tabs[_rclickTabName].Protected = checkState;

            SaveConfigsTabs();
        }

        private void UreadManageMenuItem_Click(object sender, EventArgs e)
        {
            UreadManageMenuItem.Checked = ((ToolStripMenuItem)sender).Checked;
            this.UnreadMngTbMenuItem.Checked = UreadManageMenuItem.Checked;

            if (MyCommon.IsNullOrEmpty(_rclickTabName)) return;
            ChangeTabUnreadManage(_rclickTabName, UreadManageMenuItem.Checked);

            SaveConfigsTabs();
        }

        public void ChangeTabUnreadManage(string tabName, bool isManage)
        {
            var idx = this.GetTabPageIndex(tabName);
            if (idx == -1)
                return;

            var tab = this._statuses.Tabs[tabName];
            tab.UnreadManage = isManage;

            if (SettingManager.Common.TabIconDisp)
            {
                var tabPage = this.ListTab.TabPages[idx];
                if (tab.UnreadCount > 0)
                    tabPage.ImageIndex = 0;
                else
                    tabPage.ImageIndex = -1;
            }

            if (this.CurrentTabName == tabName)
            {
                this.PurgeListViewItemCache();
                this.CurrentListView.Refresh();
            }

            SetMainWindowTitle();
            SetStatusLabelUrl();
            if (!SettingManager.Common.TabIconDisp) ListTab.Refresh();
        }

        private void NotifyDispMenuItem_Click(object sender, EventArgs e)
        {
            NotifyDispMenuItem.Checked = ((ToolStripMenuItem)sender).Checked;
            this.NotifyTbMenuItem.Checked = NotifyDispMenuItem.Checked;

            if (MyCommon.IsNullOrEmpty(_rclickTabName)) return;

            _statuses.Tabs[_rclickTabName].Notify = NotifyDispMenuItem.Checked;

            SaveConfigsTabs();
        }

        private void SoundFileComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (soundfileListup || MyCommon.IsNullOrEmpty(_rclickTabName)) return;

            _statuses.Tabs[_rclickTabName].SoundFile = (string)((ToolStripComboBox)sender).SelectedItem;

            SaveConfigsTabs();
        }

        private void DeleteTabMenuItem_Click(object sender, EventArgs e)
        {
            if (MyCommon.IsNullOrEmpty(_rclickTabName) || sender == this.DeleteTbMenuItem)
                _rclickTabName = this.CurrentTabName;

            RemoveSpecifiedTab(_rclickTabName, true);
            SaveConfigsTabs();
        }

        private void FilterEditMenuItem_Click(object sender, EventArgs e)
        {
            if (MyCommon.IsNullOrEmpty(_rclickTabName)) _rclickTabName = _statuses.HomeTab.TabName;

            using (var fltDialog = new FilterDialog())
            {
                fltDialog.Owner = this;
                fltDialog.SetCurrent(_rclickTabName);
                fltDialog.ShowDialog(this);
            }
            this.TopMost = SettingManager.Common.AlwaysTop;

            this.ApplyPostFilters();
            SaveConfigsTabs();
        }

        private async void AddTabMenuItem_Click(object sender, EventArgs e)
        {
            string? tabName = null;
            MyCommon.TabUsageType tabUsage;
            using (var inputName = new InputTabName())
            {
                inputName.TabName = _statuses.MakeTabName("MyTab");
                inputName.IsShowUsage = true;
                inputName.ShowDialog();
                if (inputName.DialogResult == DialogResult.Cancel) return;
                tabName = inputName.TabName;
                tabUsage = inputName.Usage;
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
            if (!MyCommon.IsNullOrEmpty(tabName))
            {
                //List対応
                ListElement? list = null;
                if (tabUsage == MyCommon.TabUsageType.Lists)
                {
                    using var listAvail = new ListAvailable();
                    if (listAvail.ShowDialog(this) == DialogResult.Cancel)
                        return;
                    if (listAvail.SelectedList == null)
                        return;
                    list = listAvail.SelectedList;
                }

                TabModel tab;
                switch (tabUsage)
                {
                    case MyCommon.TabUsageType.UserDefined:
                        tab = new FilterTabModel(tabName);
                        break;
                    case MyCommon.TabUsageType.PublicSearch:
                        tab = new PublicSearchTabModel(tabName);
                        break;
                    case MyCommon.TabUsageType.Lists:
                        tab = new ListTimelineTabModel(tabName, list!);
                        break;
                    default:
                        return;
                }

                if (!_statuses.AddTab(tab) || !AddNewTab(tab, startup: false))
                {
                    var tmp = string.Format(Properties.Resources.AddTabMenuItem_ClickText1, tabName);
                    MessageBox.Show(tmp, Properties.Resources.AddTabMenuItem_ClickText2, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    //成功
                    SaveConfigsTabs();

                    var tabIndex = this._statuses.Tabs.Count - 1;

                    if (tabUsage == MyCommon.TabUsageType.PublicSearch)
                    {
                        ListTab.SelectedIndex = tabIndex;
                        this.CurrentTabPage.Controls["panelSearch"].Controls["comboSearch"].Focus();
                    }
                    if (tabUsage == MyCommon.TabUsageType.Lists)
                    {
                        ListTab.SelectedIndex = tabIndex;
                        await this.RefreshTabAsync(this.CurrentTab);
                    }
                }
            }
        }

        private void TabMenuItem_Click(object sender, EventArgs e)
        {
            // 選択発言を元にフィルタ追加
            foreach (var post in this.CurrentTab.SelectedPosts)
            {
                // タブ選択（or追加）
                if (!SelectTab(out var tab))
                    return;

                using (var fltDialog = new FilterDialog())
                {
                    fltDialog.Owner = this;
                    fltDialog.SetCurrent(tab.TabName);

                    if (post.RetweetedBy == null)
                    {
                        fltDialog.AddNewFilter(post.ScreenName, post.TextFromApi);
                    }
                    else
                    {
                        fltDialog.AddNewFilter(post.RetweetedBy, post.TextFromApi);
                    }
                    fltDialog.ShowDialog(this);
                }

                this.TopMost = SettingManager.Common.AlwaysTop;
            }

            this.ApplyPostFilters();
            SaveConfigsTabs();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            //TextBox1でEnterを押してもビープ音が鳴らないようにする
            if ((keyData & Keys.KeyCode) == Keys.Enter)
            {
                if (StatusText.Focused)
                {
                    var _NewLine = false;
                    var _Post = false;

                    if (SettingManager.Common.PostCtrlEnter) //Ctrl+Enter投稿時
                    {
                        if (StatusText.Multiline)
                        {
                            if ((keyData & Keys.Shift) == Keys.Shift && (keyData & Keys.Control) != Keys.Control) _NewLine = true;

                            if ((keyData & Keys.Control) == Keys.Control) _Post = true;
                        }
                        else
                        {
                            if (((keyData & Keys.Control) == Keys.Control)) _Post = true;
                        }

                    }
                    else if (SettingManager.Common.PostShiftEnter) //SHift+Enter投稿時
                    {
                        if (StatusText.Multiline)
                        {
                            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.Shift) != Keys.Shift) _NewLine = true;

                            if ((keyData & Keys.Shift) == Keys.Shift) _Post = true;
                        }
                        else
                        {
                            if (((keyData & Keys.Shift) == Keys.Shift)) _Post = true;
                        }

                    }
                    else //Enter投稿時
                    {
                        if (StatusText.Multiline)
                        {
                            if ((keyData & Keys.Shift) == Keys.Shift && (keyData & Keys.Control) != Keys.Control) _NewLine = true;

                            if (((keyData & Keys.Control) != Keys.Control && (keyData & Keys.Shift) != Keys.Shift) ||
                                ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.Shift) == Keys.Shift)) _Post = true;
                        }
                        else
                        {
                            if (((keyData & Keys.Shift) == Keys.Shift) ||
                                (((keyData & Keys.Control) != Keys.Control) &&
                                ((keyData & Keys.Shift) != Keys.Shift))) _Post = true;
                        }
                    }

                    if (_NewLine)
                    {
                        var pos1 = StatusText.SelectionStart;
                        if (StatusText.SelectionLength > 0)
                        {
                            StatusText.Text = StatusText.Text.Remove(pos1, StatusText.SelectionLength);  //選択状態文字列削除
                        }
                        StatusText.Text = StatusText.Text.Insert(pos1, Environment.NewLine);  //改行挿入
                        StatusText.SelectionStart = pos1 + Environment.NewLine.Length;    //カーソルを改行の次の文字へ移動
                        return true;
                    }
                    else if (_Post)
                    {
                        PostButton_Click(this.PostButton, EventArgs.Empty);
                        return true;
                    }
                }
                else
                {
                    var tab = this.CurrentTab;
                    if (tab.TabType == MyCommon.TabUsageType.PublicSearch)
                    {
                        var tabPage = this.CurrentTabPage;
                        if (tabPage.Controls["panelSearch"].Controls["comboSearch"].Focused ||
                            tabPage.Controls["panelSearch"].Controls["comboLang"].Focused)
                        {
                            this.SearchButton_Click(tabPage.Controls["panelSearch"].Controls["comboSearch"], EventArgs.Empty);
                            return true;
                        }
                    }
                }
            }

            return base.ProcessDialogKey(keyData);
        }

        private void ReplyAllStripMenuItem_Click(object sender, EventArgs e)
            => this.MakeReplyOrDirectStatus(false, true, true);

        private void IDRuleMenuItem_Click(object sender, EventArgs e)
        {
            var tab = this.CurrentTab;
            var selectedPosts = tab.SelectedPosts;

            // 未選択なら処理終了
            if (selectedPosts.Length == 0)
                return;

            var screenNameArray = selectedPosts
                .Select(x => x.RetweetedBy ?? x.ScreenName)
                .ToArray();

            this.AddFilterRuleByScreenName(screenNameArray);

            if (screenNameArray.Length != 0)
            {
                var atids = new List<string>();
                foreach (var screenName in screenNameArray)
                {
                    atids.Add("@" + screenName);
                }
                var cnt = AtIdSupl.ItemCount;
                AtIdSupl.AddRangeItem(atids.ToArray());
                if (AtIdSupl.ItemCount != cnt)
                    this.MarkSettingAtIdModified();
            }
        }

        private void SourceRuleMenuItem_Click(object sender, EventArgs e)
        {
            var tab = this.CurrentTab;
            var selectedPosts = tab.SelectedPosts;

            if (selectedPosts.Length == 0)
                return;

            var sourceArray = selectedPosts.Select(x => x.Source).ToArray();

            this.AddFilterRuleBySource(sourceArray);
        }

        public void AddFilterRuleByScreenName(params string[] screenNameArray)
        {
            //タブ選択（or追加）
            if (!SelectTab(out var tab)) return;

            bool mv;
            bool mk;
            if (tab.TabType != MyCommon.TabUsageType.Mute)
            {
                this.MoveOrCopy(out mv, out mk);
            }
            else
            {
                // ミュートタブでは常に MoveMatches を true にする
                mv = true;
                mk = false;
            }

            foreach (var screenName in screenNameArray)
            {
                tab.AddFilter(new PostFilterRule
                {
                    FilterName = screenName,
                    UseNameField = true,
                    MoveMatches = mv,
                    MarkMatches = mk,
                    UseRegex = false,
                    FilterByUrl = false,
                });
            }

            this.ApplyPostFilters();
            SaveConfigsTabs();
        }

        public void AddFilterRuleBySource(params string[] sourceArray)
        {
            // タブ選択ダイアログを表示（or追加）
            if (!this.SelectTab(out var filterTab))
                return;

            bool mv;
            bool mk;
            if (filterTab.TabType != MyCommon.TabUsageType.Mute)
            {
                // フィルタ動作選択ダイアログを表示（移動/コピー, マーク有無）
                this.MoveOrCopy(out mv, out mk);
            }
            else
            {
                // ミュートタブでは常に MoveMatches を true にする
                mv = true;
                mk = false;
            }

            // 振り分けルールに追加するSource
            foreach (var source in sourceArray)
            {
                filterTab.AddFilter(new PostFilterRule
                {
                    FilterSource = source,
                    MoveMatches = mv,
                    MarkMatches = mk,
                    UseRegex = false,
                    FilterByUrl = false,
                });
            }

            this.ApplyPostFilters();
            this.SaveConfigsTabs();
        }

        private bool SelectTab([NotNullWhen(true)] out FilterTabModel? tab)
        {
            do
            {
                tab = null;

                //振り分け先タブ選択
                using (var dialog = new TabsDialog(_statuses))
                {
                    if (dialog.ShowDialog(this) == DialogResult.Cancel) return false;

                    tab = dialog.SelectedTab;
                }

                this.CurrentTabPage.Focus();
                //新規タブを選択→タブ作成
                if (tab == null)
                {
                    string tabName;
                    using (var inputName = new InputTabName())
                    {
                        inputName.TabName = _statuses.MakeTabName("MyTab");
                        inputName.ShowDialog();
                        if (inputName.DialogResult == DialogResult.Cancel) return false;
                        tabName = inputName.TabName;
                    }
                    this.TopMost = SettingManager.Common.AlwaysTop;
                    if (!MyCommon.IsNullOrEmpty(tabName))
                    {
                        var newTab = new FilterTabModel(tabName);
                        if (!_statuses.AddTab(newTab) || !AddNewTab(newTab, startup: false))
                        {
                            var tmp = string.Format(Properties.Resources.IDRuleMenuItem_ClickText2, tabName);
                            MessageBox.Show(tmp, Properties.Resources.IDRuleMenuItem_ClickText3, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            //もう一度タブ名入力
                        }
                        else
                        {
                            tab = newTab;
                            return true;
                        }
                    }
                }
                else
                {
                    //既存タブを選択
                    return true;
                }
            }
            while (true);
        }

        private void MoveOrCopy(out bool move, out bool mark)
        {
            {
                //移動するか？
                var _tmp = string.Format(Properties.Resources.IDRuleMenuItem_ClickText4, Environment.NewLine);
                if (MessageBox.Show(_tmp, Properties.Resources.IDRuleMenuItem_ClickText5, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    move = false;
                else
                    move = true;
            }
            if (!move)
            {
                //マークするか？
                var _tmp = string.Format(Properties.Resources.IDRuleMenuItem_ClickText6, Environment.NewLine);
                if (MessageBox.Show(_tmp, Properties.Resources.IDRuleMenuItem_ClickText7, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    mark = true;
                else
                    mark = false;
            }
            else
            {
                mark = false;
            }
        }

        private void CopySTOTMenuItem_Click(object sender, EventArgs e)
            => this.CopyStot();

        private void CopyURLMenuItem_Click(object sender, EventArgs e)
            => this.CopyIdUri();

        private void SelectAllMenuItem_Click(object sender, EventArgs e)
        {
            if (StatusText.Focused)
            {
                // 発言欄でのCtrl+A
                StatusText.SelectAll();
            }
            else
            {
                // ListView上でのCtrl+A
                NativeMethods.SelectAllItems(this.CurrentListView);
            }
        }

        private void MoveMiddle()
        {
            ListViewItem _item;
            int idx1;
            int idx2;

            var listView = this.CurrentListView;
            if (listView.SelectedIndices.Count == 0) return;

            var idx = listView.SelectedIndices[0];

            _item = listView.GetItemAt(0, 25);
            if (_item == null)
                idx1 = 0;
            else
                idx1 = _item.Index;

            _item = listView.GetItemAt(0, listView.ClientSize.Height - 1);
            if (_item == null)
                idx2 = listView.VirtualListSize - 1;
            else
                idx2 = _item.Index;

            idx -= Math.Abs(idx1 - idx2) / 2;
            if (idx < 0) idx = 0;

            listView.EnsureVisible(listView.VirtualListSize - 1);
            listView.EnsureVisible(idx);
        }

        private async void OpenURLMenuItem_Click(object sender, EventArgs e)
        {
            var linkElements = this.tweetDetailsView.GetLinkElements();

            if (linkElements.Length == 0)
                return;

            var links = new List<OpenUrlItem>(linkElements.Length);

            foreach (var linkElm in linkElements)
            {
                var displayUrl = linkElm.GetAttribute("title");
                var href = linkElm.GetAttribute("href");
                var linkedText = linkElm.InnerText;

                if (MyCommon.IsNullOrEmpty(displayUrl))
                    displayUrl = href;

                links.Add(new OpenUrlItem(linkedText, displayUrl, href));
            }

            string selectedUrl;
            bool isReverseSettings;

            if (links.Count == 1)
            {
                // ツイートに含まれる URL が 1 つのみの場合
                //   => OpenURL ダイアログを表示せずにリンクを開く
                selectedUrl = links[0].Href;

                // Ctrl+E で呼ばれた場合を考慮し isReverseSettings の判定を行わない
                isReverseSettings = false;
            }
            else
            {
                // ツイートに含まれる URL が複数ある場合
                //   => OpenURL を表示しユーザーが選択したリンクを開く
                this.UrlDialog.ClearUrl();

                foreach (var link in links)
                    this.UrlDialog.AddUrl(link);

                if (this.UrlDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                this.TopMost = SettingManager.Common.AlwaysTop;

                selectedUrl = this.UrlDialog.SelectedUrl;

                // Ctrlを押しながらリンクを開いた場合は、設定と逆の動作をするフラグを true としておく
                isReverseSettings = MyCommon.IsKeyDown(Keys.Control);
            }

            await this.OpenUriAsync(new Uri(selectedUrl), isReverseSettings);
        }

        private void ClearTabMenuItem_Click(object sender, EventArgs e)
        {
            if (MyCommon.IsNullOrEmpty(_rclickTabName)) return;
            ClearTab(_rclickTabName, true);
        }

        private void ClearTab(string tabName, bool showWarning)
        {
            if (showWarning)
            {
                var tmp = string.Format(Properties.Resources.ClearTabMenuItem_ClickText1, Environment.NewLine);
                if (MessageBox.Show(tmp, tabName + " " + Properties.Resources.ClearTabMenuItem_ClickText2, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                {
                    return;
                }
            }

            _statuses.ClearTabIds(tabName);
            if (this.CurrentTabName == tabName)
            {
                _anchorPost = null;
                _anchorFlag = false;
                this.PurgeListViewItemCache();
            }

            var tabIndex = this._statuses.Tabs.IndexOf(tabName);
            var tabPage = this.ListTab.TabPages[tabIndex];
            tabPage.ImageIndex = -1;

            var listView = (DetailsListView)tabPage.Tag;
            listView.VirtualListSize = 0;

            if (!SettingManager.Common.TabIconDisp) ListTab.Refresh();

            SetMainWindowTitle();
            SetStatusLabelUrl();
        }

        private static long followers = 0;

        private void SetMainWindowTitle()
        {
            //メインウインドウタイトルの書き換え
            var ttl = new StringBuilder(256);
            var ur = 0;
            var al = 0;
            if (SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.None &&
                SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.Post &&
                SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.Ver &&
                SettingManager.Common.DispLatestPost != MyCommon.DispTitleEnum.OwnStatus)
            {
                foreach (var tab in _statuses.Tabs)
                {
                    ur += tab.UnreadCount;
                    al += tab.AllCount;
                }
            }

            if (SettingManager.Common.DispUsername) ttl.Append(tw.Username).Append(" - ");
            ttl.Append(ApplicationSettings.ApplicationName);
            ttl.Append("  ");
            switch (SettingManager.Common.DispLatestPost)
            {
                case MyCommon.DispTitleEnum.Ver:
                    ttl.Append("Ver:").Append(MyCommon.GetReadableVersion());
                    break;
                case MyCommon.DispTitleEnum.Post:
                    if (_history != null && _history.Count > 1)
                        ttl.Append(_history[_history.Count - 2].status.Replace("\r\n", " "));
                    break;
                case MyCommon.DispTitleEnum.UnreadRepCount:
                    ttl.AppendFormat(Properties.Resources.SetMainWindowTitleText1, _statuses.MentionTab.UnreadCount + _statuses.DirectMessageTab.UnreadCount);
                    break;
                case MyCommon.DispTitleEnum.UnreadAllCount:
                    ttl.AppendFormat(Properties.Resources.SetMainWindowTitleText2, ur);
                    break;
                case MyCommon.DispTitleEnum.UnreadAllRepCount:
                    ttl.AppendFormat(Properties.Resources.SetMainWindowTitleText3, ur, _statuses.MentionTab.UnreadCount + _statuses.DirectMessageTab.UnreadCount);
                    break;
                case MyCommon.DispTitleEnum.UnreadCountAllCount:
                    ttl.AppendFormat(Properties.Resources.SetMainWindowTitleText4, ur, al);
                    break;
                case MyCommon.DispTitleEnum.OwnStatus:
                    if (followers == 0 && tw.FollowersCount > 0) followers = tw.FollowersCount;
                    ttl.AppendFormat(Properties.Resources.OwnStatusTitle, tw.StatusesCount, tw.FriendsCount, tw.FollowersCount, tw.FollowersCount - followers);
                    break;
            }

            try
            {
                this.Text = ttl.ToString();
            }
            catch (AccessViolationException)
            {
                //原因不明。ポスト内容に依存か？たまーに発生するが再現せず。
            }
        }

        private string GetStatusLabelText()
        {
            //ステータス欄にカウント表示
            //タブ未読数/タブ発言数 全未読数/総発言数 (未読＠＋未読DM数)
            if (_statuses == null) return "";
            var tbRep = _statuses.MentionTab;
            var tbDm = _statuses.DirectMessageTab;
            if (tbRep == null || tbDm == null) return "";
            var urat = tbRep.UnreadCount + tbDm.UnreadCount;
            var ur = 0;
            var al = 0;
            var tur = 0;
            var tal = 0;
            var slbl = new StringBuilder(256);
            try
            {
                foreach (var tab in _statuses.Tabs)
                {
                    ur += tab.UnreadCount;
                    al += tab.AllCount;
                    if (tab.TabName == this.CurrentTabName)
                    {
                        tur = tab.UnreadCount;
                        tal = tab.AllCount;
                    }
                }
            }
            catch (Exception)
            {
                return "";
            }

            UnreadCounter = ur;
            UnreadAtCounter = urat;

            var homeTab = this._statuses.HomeTab;

            slbl.AppendFormat(Properties.Resources.SetStatusLabelText1, tur, tal, ur, al, urat, _postTimestamps.Count, _favTimestamps.Count, homeTab.TweetsPerHour);
            if (SettingManager.Common.TimelinePeriod == 0)
            {
                slbl.Append(Properties.Resources.SetStatusLabelText2);
            }
            else
            {
                slbl.Append(SettingManager.Common.TimelinePeriod + Properties.Resources.SetStatusLabelText3);
            }
            return slbl.ToString();
        }

        private async void TwitterApiStatus_AccessLimitUpdated(object sender, EventArgs e)
        {
            try
            {
                if (this.InvokeRequired && !this.IsDisposed)
                {
                    await this.InvokeAsync(() => this.TwitterApiStatus_AccessLimitUpdated(sender, e));
                }
                else
                {
                    var endpointName = ((TwitterApiStatus.AccessLimitUpdatedEventArgs)e).EndpointName;
                    SetApiStatusLabel(endpointName);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }

        private void SetApiStatusLabel(string? endpointName = null)
        {
            var tabType = this.CurrentTab.TabType;

            if (endpointName == null)
            {
                // 表示中のタブに応じて更新
                endpointName = tabType switch
                {
                    MyCommon.TabUsageType.Home => "/statuses/home_timeline",
                    MyCommon.TabUsageType.UserDefined => "/statuses/home_timeline",
                    MyCommon.TabUsageType.Mentions => "/statuses/mentions_timeline",
                    MyCommon.TabUsageType.Favorites => "/favorites/list",
                    MyCommon.TabUsageType.DirectMessage => "/direct_messages/events/list",
                    MyCommon.TabUsageType.UserTimeline => "/statuses/user_timeline",
                    MyCommon.TabUsageType.Lists => "/lists/statuses",
                    MyCommon.TabUsageType.PublicSearch => "/search/tweets",
                    MyCommon.TabUsageType.Related => "/statuses/show/:id",
                    _ => null,
                };
                this.toolStripApiGauge.ApiEndpoint = endpointName;
            }
            else
            {
                // 表示中のタブに関連する endpoint であれば更新
                var update = endpointName switch
                {
                    "/statuses/home_timeline" => tabType == MyCommon.TabUsageType.Home || tabType == MyCommon.TabUsageType.UserDefined,
                    "/statuses/mentions_timeline" => tabType == MyCommon.TabUsageType.Mentions,
                    "/favorites/list" => tabType == MyCommon.TabUsageType.Favorites,
                    "/direct_messages/events/list" => tabType == MyCommon.TabUsageType.DirectMessage,
                    "/statuses/user_timeline" => tabType == MyCommon.TabUsageType.UserTimeline,
                    "/lists/statuses" => tabType == MyCommon.TabUsageType.Lists,
                    "/search/tweets" => tabType == MyCommon.TabUsageType.PublicSearch,
                    "/statuses/show/:id" => tabType == MyCommon.TabUsageType.Related,
                    _ => false,
                };
                if (update)
                {
                    this.toolStripApiGauge.ApiEndpoint = endpointName;
                }
            }
        }

        private void SetStatusLabelUrl()
            => this.StatusLabelUrl.Text = this.GetStatusLabelText();

        public void SetStatusLabel(string text)
            => this.StatusLabel.Text = text;

        private void SetNotifyIconText()
        {
            var ur = new StringBuilder(64);

            // タスクトレイアイコンのツールチップテキスト書き換え
            // Tween [未読/@]
            ur.Remove(0, ur.Length);
            if (SettingManager.Common.DispUsername)
            {
                ur.Append(tw.Username);
                ur.Append(" - ");
            }
            ur.Append(ApplicationSettings.ApplicationName);
#if DEBUG
            ur.Append("(Debug Build)");
#endif
            if (UnreadCounter != -1 && UnreadAtCounter != -1)
            {
                ur.Append(" [");
                ur.Append(UnreadCounter);
                ur.Append("/@");
                ur.Append(UnreadAtCounter);
                ur.Append("]");
            }
            NotifyIcon1.Text = ur.ToString();
        }

        internal void CheckReplyTo(string StatusText)
        {
            MatchCollection m;
            //ハッシュタグの保存
            m = Regex.Matches(StatusText, Twitter.HASHTAG, RegexOptions.IgnoreCase);
            var hstr = "";
            foreach (Match hm in m)
            {
                if (!hstr.Contains("#" + hm.Result("$3") + " "))
                {
                    hstr += "#" + hm.Result("$3") + " ";
                    HashSupl.AddItem("#" + hm.Result("$3"));
                }
            }
            if (!MyCommon.IsNullOrEmpty(HashMgr.UseHash) && !hstr.Contains(HashMgr.UseHash + " "))
            {
                hstr += HashMgr.UseHash;
            }
            if (!MyCommon.IsNullOrEmpty(hstr)) HashMgr.AddHashToHistory(hstr.Trim(), false);

            // 本当にリプライ先指定すべきかどうかの判定
            m = Regex.Matches(StatusText, "(^|[ -/:-@[-^`{-~])(?<id>@[a-zA-Z0-9_]+)");

            if (SettingManager.Common.UseAtIdSupplement)
            {
                var bCnt = AtIdSupl.ItemCount;
                foreach (Match mid in m)
                {
                    AtIdSupl.AddItem(mid.Result("${id}"));
                }
                if (bCnt != AtIdSupl.ItemCount)
                    this.MarkSettingAtIdModified();
            }

            // リプライ先ステータスIDの指定がない場合は指定しない
            if (this.inReplyTo == null)
                return;

            // 通常Reply
            // 次の条件を満たす場合に in_reply_to_status_id 指定
            // 1. Twitterによりリンクと判定される @idが文中に1つ含まれる (2009/5/28 リンク化される@IDのみカウントするように修正)
            // 2. リプライ先ステータスIDが設定されている(リストをダブルクリックで返信している)
            // 3. 文中に含まれた@idがリプライ先のポスト者のIDと一致する

            if (m != null)
            {
                var inReplyToScreenName = this.inReplyTo.Value.ScreenName;
                if (StatusText.StartsWith("@", StringComparison.Ordinal))
                {
                    if (StatusText.StartsWith("@" + inReplyToScreenName, StringComparison.Ordinal)) return;
                }
                else
                {
                    foreach (Match mid in m)
                    {
                        if (StatusText.Contains("RT " + mid.Result("${id}") + ":") && mid.Result("${id}") == "@" + inReplyToScreenName) return;
                    }
                }
            }

            this.inReplyTo = null;
        }

        private void TweenMain_Resize(object sender, EventArgs e)
        {
            if (!_initialLayout && SettingManager.Common.MinimizeToTray && WindowState == FormWindowState.Minimized)
            {
                this.Visible = false;
            }
            if (_initialLayout && SettingManager.Local != null && this.WindowState == FormWindowState.Normal && this.Visible)
            {
                // 現在の DPI と設定保存時の DPI との比を取得する
                var configScaleFactor = SettingManager.Local.GetConfigScaleFactor(this.CurrentAutoScaleDimensions);

                this.ClientSize = ScaleBy(configScaleFactor, SettingManager.Local.FormSize);

                // Splitterの位置設定
                var splitterDistance = ScaleBy(configScaleFactor.Height, SettingManager.Local.SplitterDistance);
                if (splitterDistance > this.SplitContainer1.Panel1MinSize &&
                    splitterDistance < this.SplitContainer1.Height - this.SplitContainer1.Panel2MinSize - this.SplitContainer1.SplitterWidth)
                {
                    this.SplitContainer1.SplitterDistance = splitterDistance;
                }

                //発言欄複数行
                StatusText.Multiline = SettingManager.Local.StatusMultiline;
                if (StatusText.Multiline)
                {
                    var statusTextHeight = ScaleBy(configScaleFactor.Height, SettingManager.Local.StatusTextHeight);
                    var dis = SplitContainer2.Height - statusTextHeight - SplitContainer2.SplitterWidth;
                    if (dis > SplitContainer2.Panel1MinSize && dis < SplitContainer2.Height - SplitContainer2.Panel2MinSize - SplitContainer2.SplitterWidth)
                    {
                        SplitContainer2.SplitterDistance = SplitContainer2.Height - statusTextHeight - SplitContainer2.SplitterWidth;
                    }
                    StatusText.Height = statusTextHeight;
                }
                else
                {
                    if (SplitContainer2.Height - SplitContainer2.Panel2MinSize - SplitContainer2.SplitterWidth > 0)
                    {
                        SplitContainer2.SplitterDistance = SplitContainer2.Height - SplitContainer2.Panel2MinSize - SplitContainer2.SplitterWidth;
                    }
                }

                var previewDistance = ScaleBy(configScaleFactor.Width, SettingManager.Local.PreviewDistance);
                if (previewDistance > this.SplitContainer3.Panel1MinSize && previewDistance < this.SplitContainer3.Width - this.SplitContainer3.Panel2MinSize - this.SplitContainer3.SplitterWidth)
                {
                    this.SplitContainer3.SplitterDistance = previewDistance;
                }

                // Panel2Collapsed は SplitterDistance の設定を終えるまで true にしない
                this.SplitContainer3.Panel2Collapsed = true;

                _initialLayout = false;
            }
            if (this.WindowState != FormWindowState.Minimized)
            {
                _formWindowState = this.WindowState;
            }
        }

        private void PlaySoundMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            PlaySoundMenuItem.Checked = ((ToolStripMenuItem)sender).Checked;
            this.PlaySoundFileMenuItem.Checked = PlaySoundMenuItem.Checked;
            if (PlaySoundMenuItem.Checked)
            {
                SettingManager.Common.PlaySound = true;
            }
            else
            {
                SettingManager.Common.PlaySound = false;
            }
            this.MarkSettingCommonModified();
        }

        private void SplitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (this._initialLayout)
                return;

            int splitterDistance;
            switch (this.WindowState)
            {
                case FormWindowState.Normal:
                    splitterDistance = this.SplitContainer1.SplitterDistance;
                    break;
                case FormWindowState.Maximized:
                    // 最大化時は、通常時のウィンドウサイズに換算した SplitterDistance を算出する
                    var normalContainerHeight = this._mySize.Height - this.ToolStripContainer1.TopToolStripPanel.Height - this.ToolStripContainer1.BottomToolStripPanel.Height;
                    splitterDistance = this.SplitContainer1.SplitterDistance - (this.SplitContainer1.Height - normalContainerHeight);
                    splitterDistance = Math.Min(splitterDistance, normalContainerHeight - this.SplitContainer1.SplitterWidth - this.SplitContainer1.Panel2MinSize);
                    break;
                default:
                    return;
            }

            this._mySpDis = splitterDistance;
            this.MarkSettingLocalModified();
        }

        private async Task doRepliedStatusOpen()
        {
            var currentPost = this.CurrentPost;
            if (this.ExistCurrentPost && currentPost != null && currentPost.InReplyToUser != null && currentPost.InReplyToStatusId != null)
            {
                if (MyCommon.IsKeyDown(Keys.Shift))
                {
                    await this.OpenUriInBrowserAsync(MyCommon.GetStatusUrl(currentPost.InReplyToUser, currentPost.InReplyToStatusId.Value));
                    return;
                }
                if (this._statuses.Posts.TryGetValue(currentPost.InReplyToStatusId.Value, out var repPost))
                {
                    MessageBox.Show($"{repPost.ScreenName} / {repPost.Nickname}   ({repPost.CreatedAt.ToLocalTimeString()})" + Environment.NewLine + repPost.TextFromApi);
                }
                else
                {
                    foreach (var tb in _statuses.GetTabsByType(MyCommon.TabUsageType.Lists | MyCommon.TabUsageType.PublicSearch))
                    {
                        if (tb == null || !tb.Contains(currentPost.InReplyToStatusId.Value)) break;
                        repPost = tb.Posts[currentPost.InReplyToStatusId.Value];
                        MessageBox.Show($"{repPost.ScreenName} / {repPost.Nickname}   ({repPost.CreatedAt.ToLocalTimeString()})" + Environment.NewLine + repPost.TextFromApi);
                        return;
                    }
                    await this.OpenUriInBrowserAsync(MyCommon.GetStatusUrl(currentPost.InReplyToUser, currentPost.InReplyToStatusId.Value));
                }
            }
        }

        private async void RepliedStatusOpenMenuItem_Click(object sender, EventArgs e)
            => await this.doRepliedStatusOpen();

        private void SplitContainer2_Panel2_Resize(object sender, EventArgs e)
        {
            if (this._initialLayout)
                return; // SettingLocal の反映が完了するまで multiline の判定を行わない

            var multiline = this.SplitContainer2.Panel2.Height > this.SplitContainer2.Panel2MinSize + 2;
            if (multiline != this.StatusText.Multiline)
            {
                this.StatusText.Multiline = multiline;
                SettingManager.Local.StatusMultiline = multiline;
                this.MarkSettingLocalModified();
            }
        }

        private void StatusText_MultilineChanged(object sender, EventArgs e)
        {
            if (this.StatusText.Multiline)
                this.StatusText.ScrollBars = ScrollBars.Vertical;
            else
                this.StatusText.ScrollBars = ScrollBars.None;

            if (!this._initialLayout)
                this.MarkSettingLocalModified();
        }

        private void MultiLineMenuItem_Click(object sender, EventArgs e)
        {
            //発言欄複数行
            var menuItemChecked = ((ToolStripMenuItem)sender).Checked;
            StatusText.Multiline = menuItemChecked;
            SettingManager.Local.StatusMultiline = menuItemChecked;
            if (menuItemChecked)
            {
                if (SplitContainer2.Height - _mySpDis2 - SplitContainer2.SplitterWidth < 0)
                    SplitContainer2.SplitterDistance = 0;
                else
                    SplitContainer2.SplitterDistance = SplitContainer2.Height - _mySpDis2 - SplitContainer2.SplitterWidth;
            }
            else
            {
                SplitContainer2.SplitterDistance = SplitContainer2.Height - SplitContainer2.Panel2MinSize - SplitContainer2.SplitterWidth;
            }
            this.MarkSettingLocalModified();
        }

        private async Task<bool> UrlConvertAsync(MyCommon.UrlConverter Converter_Type)
        {
            if (Converter_Type == MyCommon.UrlConverter.Bitly || Converter_Type == MyCommon.UrlConverter.Jmp)
            {
                // OAuth2 アクセストークンまたは API キー (旧方式) のいずれも設定されていなければ短縮しない
                if (MyCommon.IsNullOrEmpty(SettingManager.Common.BitlyAccessToken) &&
                    (MyCommon.IsNullOrEmpty(SettingManager.Common.BilyUser) || MyCommon.IsNullOrEmpty(SettingManager.Common.BitlyPwd)))
                {
                    MessageBox.Show(this, Properties.Resources.UrlConvert_BitlyAuthRequired, ApplicationSettings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            //Converter_Type=Nicomsの場合は、nicovideoのみ短縮する
            //参考資料 RFC3986 Uniform Resource Identifier (URI): Generic Syntax
            //Appendix A.  Collected ABNF for URI
            //http://www.ietf.org/rfc/rfc3986.txt

            const string nico = @"^https?://[a-z]+\.(nicovideo|niconicommons|nicolive)\.jp/[a-z]+/[a-z0-9]+$";

            string result;
            if (StatusText.SelectionLength > 0)
            {
                var tmp = StatusText.SelectedText;
                // httpから始まらない場合、ExcludeStringで指定された文字列で始まる場合は対象としない
                if (tmp.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // 文字列が選択されている場合はその文字列について処理

                    //nico.ms使用、nicovideoにマッチしたら変換
                    if (SettingManager.Common.Nicoms && Regex.IsMatch(tmp, nico))
                    {
                        result = nicoms.Shorten(tmp);
                    }
                    else if (Converter_Type != MyCommon.UrlConverter.Nicoms)
                    {
                        // 短縮URL変換
                        try
                        {
                            var srcUri = new Uri(tmp);
                            var resultUri = await ShortUrl.Instance.ShortenUrlAsync(Converter_Type, srcUri);
                            result = resultUri.AbsoluteUri;
                        }
                        catch (WebApiException e)
                        {
                            this.StatusLabel.Text = Converter_Type + ":" + e.Message;
                            return false;
                        }
                        catch (UriFormatException e)
                        {
                            this.StatusLabel.Text = Converter_Type + ":" + e.Message;
                            return false;
                        }
                    }
                    else
                    {
                        return true;
                    }

                    if (!MyCommon.IsNullOrEmpty(result))
                    {
                        var undotmp = new urlUndo();

                        // 短縮 URL が生成されるまでの間に投稿欄から元の URL が削除されていたら中断する
                        var origUrlIndex = this.StatusText.Text.IndexOf(tmp, StringComparison.Ordinal);
                        if (origUrlIndex == -1)
                            return false;

                        StatusText.Select(origUrlIndex, tmp.Length);
                        StatusText.SelectedText = result;

                        //undoバッファにセット
                        undotmp.Before = tmp;
                        undotmp.After = result;

                        if (urlUndoBuffer == null)
                        {
                            urlUndoBuffer = new List<urlUndo>();
                            UrlUndoToolStripMenuItem.Enabled = true;
                        }

                        urlUndoBuffer.Add(undotmp);
                    }
                }
            }
            else
            {
                const string url = @"(?<before>(?:[^\""':!=]|^|\:))" +
                                   @"(?<url>(?<protocol>https?://)" +
                                   @"(?<domain>(?:[\.-]|[^\p{P}\s])+\.[a-z]{2,}(?::[0-9]+)?)" +
                                   @"(?<path>/[a-z0-9!*//();:&=+$/%#\-_.,~@]*[a-z0-9)=#/]?)?" +
                                   @"(?<query>\?[a-z0-9!*//();:&=+$/%#\-_.,~@?]*[a-z0-9_&=#/])?)";
                // 正規表現にマッチしたURL文字列をtinyurl化
                foreach (Match mt in Regex.Matches(StatusText.Text, url, RegexOptions.IgnoreCase))
                {
                    if (StatusText.Text.IndexOf(mt.Result("${url}"), StringComparison.Ordinal) == -1)
                        continue;
                    var tmp = mt.Result("${url}");
                    if (tmp.StartsWith("w", StringComparison.OrdinalIgnoreCase))
                        tmp = "http://" + tmp;
                    var undotmp = new urlUndo();

                    //選んだURLを選択（？）
                    StatusText.Select(StatusText.Text.IndexOf(mt.Result("${url}"), StringComparison.Ordinal), mt.Result("${url}").Length);

                    //nico.ms使用、nicovideoにマッチしたら変換
                    if (SettingManager.Common.Nicoms && Regex.IsMatch(tmp, nico))
                    {
                        result = nicoms.Shorten(tmp);
                    }
                    else if (Converter_Type != MyCommon.UrlConverter.Nicoms)
                    {
                        // 短縮URL変換
                        try
                        {
                            var srcUri = new Uri(tmp);
                            var resultUri = await ShortUrl.Instance.ShortenUrlAsync(Converter_Type, srcUri);
                            result = resultUri.AbsoluteUri;
                        }
                        catch (HttpRequestException e)
                        {
                            // 例外のメッセージが「Response status code does not indicate success: 500 (Internal Server Error).」
                            // のように長いので「:」が含まれていればそれ以降のみを抽出する
                            var message = e.Message.Split(new[] { ':' }, count: 2).Last();

                            this.StatusLabel.Text = Converter_Type + ":" + message;
                            continue;
                        }
                        catch (WebApiException e)
                        {
                            this.StatusLabel.Text = Converter_Type + ":" + e.Message;
                            continue;
                        }
                        catch (UriFormatException e)
                        {
                            this.StatusLabel.Text = Converter_Type + ":" + e.Message;
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    if (!MyCommon.IsNullOrEmpty(result))
                    {
                        // 短縮 URL が生成されるまでの間に投稿欄から元の URL が削除されていたら中断する
                        var origUrlIndex = this.StatusText.Text.IndexOf(mt.Result("${url}"), StringComparison.Ordinal);
                        if (origUrlIndex == -1)
                            return false;

                        StatusText.Select(origUrlIndex, mt.Result("${url}").Length);
                        StatusText.SelectedText = result;
                        //undoバッファにセット
                        undotmp.Before = mt.Result("${url}");
                        undotmp.After = result;

                        if (urlUndoBuffer == null)
                        {
                            urlUndoBuffer = new List<urlUndo>();
                            UrlUndoToolStripMenuItem.Enabled = true;
                        }

                        urlUndoBuffer.Add(undotmp);
                    }
                }
            }

            return true;
        }

        private void doUrlUndo()
        {
            if (urlUndoBuffer != null)
            {
                var tmp = StatusText.Text;
                foreach (var data in urlUndoBuffer)
                {
                    tmp = tmp.Replace(data.After, data.Before);
                }
                StatusText.Text = tmp;
                urlUndoBuffer = null;
                UrlUndoToolStripMenuItem.Enabled = false;
                StatusText.SelectionStart = 0;
                StatusText.SelectionLength = 0;
            }
        }

        private async void TinyURLToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.UrlConvertAsync(MyCommon.UrlConverter.TinyUrl);

        private async void IsgdToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.UrlConvertAsync(MyCommon.UrlConverter.Isgd);

        private async void UxnuMenuItem_Click(object sender, EventArgs e)
            => await this.UrlConvertAsync(MyCommon.UrlConverter.Uxnu);

        private async void UrlConvertAutoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!await UrlConvertAsync(SettingManager.Common.AutoShortUrlFirst))
            {
                var rnd = new Random();

                MyCommon.UrlConverter svc;
                // 前回使用した短縮URLサービス以外を選択する
                do
                {
                    svc = (MyCommon.UrlConverter)rnd.Next(System.Enum.GetNames(typeof(MyCommon.UrlConverter)).Length);
                }
                while (svc == SettingManager.Common.AutoShortUrlFirst || svc == MyCommon.UrlConverter.Nicoms || svc == MyCommon.UrlConverter.Unu);
                await UrlConvertAsync(svc);
            }
        }

        private void UrlUndoToolStripMenuItem_Click(object sender, EventArgs e)
            => this.doUrlUndo();

        private void NewPostPopMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            this.NotifyFileMenuItem.Checked = ((ToolStripMenuItem)sender).Checked;
            this.NewPostPopMenuItem.Checked = this.NotifyFileMenuItem.Checked;
            SettingManager.Common.NewAllPop = NewPostPopMenuItem.Checked;
            this.MarkSettingCommonModified();
        }

        private void ListLockMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            ListLockMenuItem.Checked = ((ToolStripMenuItem)sender).Checked;
            this.LockListFileMenuItem.Checked = ListLockMenuItem.Checked;
            SettingManager.Common.ListLock = ListLockMenuItem.Checked;
            this.MarkSettingCommonModified();
        }

        private void MenuStrip1_MenuActivate(object sender, EventArgs e)
        {
            // フォーカスがメニューに移る (MenuStrip1.Tag フラグを立てる)
            MenuStrip1.Tag = new object();
            MenuStrip1.Select(); // StatusText がフォーカスを持っている場合 Leave が発生
        }

        private void MenuStrip1_MenuDeactivate(object sender, EventArgs e)
        {
            var currentTabPage = this.CurrentTabPage;
            if (this.Tag != null) // 設定された戻り先へ遷移
            {
                if (this.Tag == currentTabPage)
                    ((Control)currentTabPage.Tag).Select();
                else
                    ((Control)this.Tag).Select();
            }
            else // 戻り先が指定されていない (初期状態) 場合はタブに遷移
            {
                this.Tag = currentTabPage.Tag;
                ((Control)this.Tag).Select();
            }
            // フォーカスがメニューに遷移したかどうかを表すフラグを降ろす
            MenuStrip1.Tag = null;
        }

        private void MyList_ColumnReordered(object sender, ColumnReorderedEventArgs e)
        {
            var lst = (DetailsListView)sender;
            if (SettingManager.Local == null) return;

            if (_iconCol)
            {
                SettingManager.Local.Width1 = lst.Columns[0].Width;
                SettingManager.Local.Width3 = lst.Columns[1].Width;
            }
            else
            {
                var darr = new int[lst.Columns.Count];
                for (var i = 0; i < lst.Columns.Count; i++)
                {
                    darr[lst.Columns[i].DisplayIndex] = i;
                }
                MyCommon.MoveArrayItem(darr, e.OldDisplayIndex, e.NewDisplayIndex);

                for (var i = 0; i < lst.Columns.Count; i++)
                {
                    switch (darr[i])
                    {
                        case 0:
                            SettingManager.Local.DisplayIndex1 = i;
                            break;
                        case 1:
                            SettingManager.Local.DisplayIndex2 = i;
                            break;
                        case 2:
                            SettingManager.Local.DisplayIndex3 = i;
                            break;
                        case 3:
                            SettingManager.Local.DisplayIndex4 = i;
                            break;
                        case 4:
                            SettingManager.Local.DisplayIndex5 = i;
                            break;
                        case 5:
                            SettingManager.Local.DisplayIndex6 = i;
                            break;
                        case 6:
                            SettingManager.Local.DisplayIndex7 = i;
                            break;
                        case 7:
                            SettingManager.Local.DisplayIndex8 = i;
                            break;
                    }
                }
                SettingManager.Local.Width1 = lst.Columns[0].Width;
                SettingManager.Local.Width2 = lst.Columns[1].Width;
                SettingManager.Local.Width3 = lst.Columns[2].Width;
                SettingManager.Local.Width4 = lst.Columns[3].Width;
                SettingManager.Local.Width5 = lst.Columns[4].Width;
                SettingManager.Local.Width6 = lst.Columns[5].Width;
                SettingManager.Local.Width7 = lst.Columns[6].Width;
                SettingManager.Local.Width8 = lst.Columns[7].Width;
            }
            this.MarkSettingLocalModified();
            _isColumnChanged = true;
        }

        private void MyList_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            var lst = (DetailsListView)sender;
            if (SettingManager.Local == null) return;

            var modified = false;
            if (_iconCol)
            {
                if (SettingManager.Local.Width1 != lst.Columns[0].Width)
                {
                    SettingManager.Local.Width1 = lst.Columns[0].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width3 != lst.Columns[1].Width)
                {
                    SettingManager.Local.Width3 = lst.Columns[1].Width;
                    modified = true;
                }
            }
            else
            {
                if (SettingManager.Local.Width1 != lst.Columns[0].Width)
                {
                    SettingManager.Local.Width1 = lst.Columns[0].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width2 != lst.Columns[1].Width)
                {
                    SettingManager.Local.Width2 = lst.Columns[1].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width3 != lst.Columns[2].Width)
                {
                    SettingManager.Local.Width3 = lst.Columns[2].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width4 != lst.Columns[3].Width)
                {
                    SettingManager.Local.Width4 = lst.Columns[3].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width5 != lst.Columns[4].Width)
                {
                    SettingManager.Local.Width5 = lst.Columns[4].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width6 != lst.Columns[5].Width)
                {
                    SettingManager.Local.Width6 = lst.Columns[5].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width7 != lst.Columns[6].Width)
                {
                    SettingManager.Local.Width7 = lst.Columns[6].Width;
                    modified = true;
                }
                if (SettingManager.Local.Width8 != lst.Columns[7].Width)
                {
                    SettingManager.Local.Width8 = lst.Columns[7].Width;
                    modified = true;
                }
            }
            if (modified)
            {
                this.MarkSettingLocalModified();
                this._isColumnChanged = true;
            }
        }

        private void SplitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (StatusText.Multiline) _mySpDis2 = StatusText.Height;
            this.MarkSettingLocalModified();
        }

        private void TweenMain_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (!e.Data.GetDataPresent(DataFormats.Html, false))  // WebBrowserコントロールからの絵文字画像Drag&Dropは弾く
                {
                    SelectMedia_DragDrop(e);
                }
            }
            else if (e.Data.GetDataPresent("UniformResourceLocatorW"))
            {
                var (url, title) = GetUrlFromDataObject(e.Data);

                string appendText;
                if (title == null)
                    appendText = url;
                else
                    appendText = title + " " + url;

                if (this.StatusText.TextLength == 0)
                    this.StatusText.Text = appendText;
                else
                    this.StatusText.Text += " " + appendText;
            }
            else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                var text = (string)e.Data.GetData(DataFormats.UnicodeText);
                if (text != null)
                    this.StatusText.Text += text;
            }
            else if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                var data = (string)e.Data.GetData(DataFormats.StringFormat, true);
                if (data != null) StatusText.Text += data;
            }
        }

        /// <summary>
        /// IDataObject から URL とタイトルの対を取得します
        /// </summary>
        /// <remarks>
        /// タイトルのみ取得できなかった場合は Value2 が null のタプルを返すことがあります。
        /// </remarks>
        /// <exception cref="ArgumentException">不正なフォーマットが入力された場合</exception>
        /// <exception cref="NotSupportedException">サポートされていないデータが入力された場合</exception>
        internal static (string Url, string? Title) GetUrlFromDataObject(IDataObject data)
        {
            if (data.GetDataPresent("text/x-moz-url"))
            {
                // Firefox, Google Chrome で利用可能
                // 参照: https://developer.mozilla.org/ja/docs/DragDrop/Recommended_Drag_Types

                using var stream = (MemoryStream)data.GetData("text/x-moz-url");
                var lines = Encoding.Unicode.GetString(stream.ToArray()).TrimEnd('\0').Split('\n');
                if (lines.Length < 2)
                    throw new ArgumentException("不正な text/x-moz-url フォーマットです", nameof(data));

                return (lines[0], lines[1]);
            }
            else if (data.GetDataPresent("IESiteModeToUrl"))
            {
                // Internet Exproler 用
                // 保護モードが有効なデフォルトの IE では DragDrop イベントが発火しないため使えない

                using var stream = (MemoryStream)data.GetData("IESiteModeToUrl");
                var lines = Encoding.Unicode.GetString(stream.ToArray()).TrimEnd('\0').Split('\0');
                if (lines.Length < 2)
                    throw new ArgumentException("不正な IESiteModeToUrl フォーマットです", nameof(data));

                return (lines[0], lines[1]);
            }
            else if (data.GetDataPresent("UniformResourceLocatorW"))
            {
                // それ以外のブラウザ向け

                using var stream = (MemoryStream)data.GetData("UniformResourceLocatorW");
                var url = Encoding.Unicode.GetString(stream.ToArray()).TrimEnd('\0');
                return (url, null);
            }

            throw new NotSupportedException("サポートされていないデータ形式です: " + data.GetFormats()[0]);
        }

        private void TweenMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (!e.Data.GetDataPresent(DataFormats.Html, false))  // WebBrowserコントロールからの絵文字画像Drag&Dropは弾く
                {
                    SelectMedia_DragEnter(e);
                    return;
                }
            }
            else if (e.Data.GetDataPresent("UniformResourceLocatorW"))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
            else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
            else if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void TweenMain_DragOver(object sender, DragEventArgs e)
        {
        }

        public bool IsNetworkAvailable()
        {
            var nw = MyCommon.IsNetworkAvailable();
            _myStatusOnline = nw;
            return nw;
        }

        public async Task OpenUriAsync(Uri uri, bool isReverseSettings = false)
        {
            var uriStr = uri.AbsoluteUri;

            // OpenTween 内部で使用する URL
            if (uri.Authority == "opentween")
            {
                await this.OpenInternalUriAsync(uri);
                return;
            }

            // ハッシュタグを含む Twitter 検索
            if (uri.Host == "twitter.com" && uri.AbsolutePath == "/search" && uri.Query.Contains("q=%23"))
            {
                // ハッシュタグの場合は、タブで開く
                var unescapedQuery = Uri.UnescapeDataString(uri.Query);
                var pos = unescapedQuery.IndexOf('#');
                if (pos == -1) return;

                var hash = unescapedQuery.Substring(pos);
                this.HashSupl.AddItem(hash);
                this.HashMgr.AddHashToHistory(hash.Trim(), false);
                this.AddNewTabForSearch(hash);
                return;
            }

            // ユーザープロフィールURL
            // フラグが立っている場合は設定と逆の動作をする
            if( SettingManager.Common.OpenUserTimeline && !isReverseSettings ||
                !SettingManager.Common.OpenUserTimeline && isReverseSettings )
            {
                var userUriMatch = Regex.Match(uriStr, "^https?://twitter.com/(#!/)?(?<ScreenName>[a-zA-Z0-9_]+)$");
                if (userUriMatch.Success)
                {
                    var screenName = userUriMatch.Groups["ScreenName"].Value;
                    if (this.IsTwitterId(screenName))
                    {
                        await this.AddNewTabForUserTimeline(screenName);
                        return;
                    }
                }
            }

            // どのパターンにも該当しないURL
            await this.OpenUriInBrowserAsync(uriStr);
        }

        /// <summary>
        /// OpenTween 内部の機能を呼び出すための URL を開きます
        /// </summary>
        private async Task OpenInternalUriAsync(Uri uri)
        {
            // ツイートを開く (//opentween/status/:status_id)
            var match = Regex.Match(uri.AbsolutePath, @"^/status/(\d+)$");
            if (match.Success)
            {
                var statusId = long.Parse(match.Groups[1].Value);
                await this.OpenRelatedTab(statusId);
                return;
            }
        }

        public Task OpenUriInBrowserAsync(string UriString)
        {
            return Task.Run(() =>
            {
                var myPath = UriString;

                try
                {
                    var configBrowserPath = SettingManager.Local.BrowserPath;
                    if (!MyCommon.IsNullOrEmpty(configBrowserPath))
                    {
                        if (configBrowserPath.StartsWith("\"", StringComparison.Ordinal) && configBrowserPath.Length > 2 && configBrowserPath.IndexOf("\"", 2, StringComparison.Ordinal) > -1)
                        {
                            var sep = configBrowserPath.IndexOf("\"", 2, StringComparison.Ordinal);
                            var browserPath = configBrowserPath.Substring(1, sep - 1);
                            var arg = "";
                            if (sep < configBrowserPath.Length - 1)
                            {
                                arg = configBrowserPath.Substring(sep + 1);
                            }
                            myPath = arg + " " + myPath;
                            System.Diagnostics.Process.Start(browserPath, myPath);
                        }
                        else
                        {
                            System.Diagnostics.Process.Start(configBrowserPath, myPath);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Process.Start(myPath);
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        private void ListTabSelect(TabPage _tab)
        {
            SetListProperty();

            this.PurgeListViewItemCache();

            this._statuses.SelectTab(_tab.Text);

            var listView = this.CurrentListView;

            _anchorPost = null;
            _anchorFlag = false;

            if (_iconCol)
            {
                listView.Columns[1].Text = ColumnText[2];
            }
            else
            {
                for (var i = 0; i < listView.Columns.Count; i++)
                {
                    listView.Columns[i].Text = ColumnText[i];
                }
            }
        }

        private void ListTab_Selecting(object sender, TabControlCancelEventArgs e)
            => this.ListTabSelect(e.TabPage);

        private void SelectListItem(DetailsListView LView, int Index)
        {
            //単一
            var bnd = new Rectangle();
            var flg = false;
            var item = LView.FocusedItem;
            if (item != null)
            {
                bnd = item.Bounds;
                flg = true;
            }

            do
            {
                LView.SelectedIndices.Clear();
            }
            while (LView.SelectedIndices.Count > 0);
            item = LView.Items[Index];
            item.Selected = true;
            item.Focused = true;

            if (flg) LView.Invalidate(bnd);
        }

        private void SelectListItem(DetailsListView LView , int[]? Index, int focusedIndex, int selectionMarkIndex)
        {
            //複数
            var bnd = new Rectangle();
            var flg = false;
            var item = LView.FocusedItem;
            if (item != null)
            {
                bnd = item.Bounds;
                flg = true;
            }

            if (Index != null)
            {
                LView.SelectItems(Index);
            }
            if (selectionMarkIndex > -1 && LView.VirtualListSize > selectionMarkIndex)
            {
                LView.SelectionMark = selectionMarkIndex;
            }
            if (focusedIndex > -1 && LView.VirtualListSize > focusedIndex)
            {
                LView.Items[focusedIndex].Focused = true;
            }
            else if (Index != null && Index.Length != 0)
            {
                LView.Items[Index.Last()].Focused = true;
            }

            if (flg) LView.Invalidate(bnd);
        }

        private void StartUserStream()
        {
            tw.NewPostFromStream += tw_NewPostFromStream;
            tw.UserStreamStarted += tw_UserStreamStarted;
            tw.UserStreamStopped += tw_UserStreamStopped;
            tw.PostDeleted += tw_PostDeleted;
            tw.UserStreamEventReceived += tw_UserStreamEventArrived;

            this.RefreshUserStreamsMenu();

            if (SettingManager.Common.UserstreamStartup)
                tw.StartUserStream();
        }

        private async void TweenMain_Shown(object sender, EventArgs e)
        {
            NotifyIcon1.Visible = true;

            if (this.IsNetworkAvailable())
            {
                StartUserStream();

                var loadTasks = new List<Task>
                {
                    this.RefreshMuteUserIdsAsync(),
                    this.RefreshBlockIdsAsync(),
                    this.RefreshNoRetweetIdsAsync(),
                    this.RefreshTwitterConfigurationAsync(),
                    this.RefreshTabAsync<HomeTabModel>(),
                    this.RefreshTabAsync<MentionsTabModel>(),
                    this.RefreshTabAsync<DirectMessagesTabModel>(),
                    this.RefreshTabAsync<PublicSearchTabModel>(),
                    this.RefreshTabAsync<UserTimelineTabModel>(),
                    this.RefreshTabAsync<ListTimelineTabModel>(),
                };

                if (SettingManager.Common.StartupFollowers)
                    loadTasks.Add(this.RefreshFollowerIdsAsync());

                if (SettingManager.Common.GetFav)
                    loadTasks.Add(this.RefreshTabAsync<FavoritesTabModel>());

                var allTasks = Task.WhenAll(loadTasks);

                var i = 0;
                while (true)
                {
                    var timeout = Task.Delay(5000);
                    if (await Task.WhenAny(allTasks, timeout) != timeout)
                        break;

                    i += 1;
                    if (i > 24) break; // 120秒間初期処理が終了しなかったら強制的に打ち切る

                    if (MyCommon._endingFlag)
                        return;
                }

                if (MyCommon._endingFlag) return;

                if (ApplicationSettings.VersionInfoUrl != null)
                {
                    //バージョンチェック（引数：起動時チェックの場合はtrue･･･チェック結果のメッセージを表示しない）
                    if (SettingManager.Common.StartupVersion)
                        await this.CheckNewVersion(true);
                }
                else
                {
                    // ApplicationSetting.cs の設定により更新チェックが無効化されている場合
                    this.VerUpMenuItem.Enabled = false;
                    this.VerUpMenuItem.Available = false;
                    this.ToolStripSeparator16.Available = false; // VerUpMenuItem の一つ上にあるセパレータ
                }

                // 権限チェック read/write権限(xAuthで取得したトークン)の場合は再認証を促す
                if (MyCommon.TwitterApiInfo.AccessLevel == TwitterApiAccessLevel.ReadWrite)
                {
                    MessageBox.Show(Properties.Resources.ReAuthorizeText);
                    SettingStripMenuItem_Click(this.SettingStripMenuItem, EventArgs.Empty);
                }

                // 取得失敗の場合は再試行する
                var reloadTasks = new List<Task>();

                if (!tw.GetFollowersSuccess && SettingManager.Common.StartupFollowers)
                    reloadTasks.Add(this.RefreshFollowerIdsAsync());

                if (!tw.GetNoRetweetSuccess)
                    reloadTasks.Add(this.RefreshNoRetweetIdsAsync());

                if (this.tw.Configuration.PhotoSizeLimit == 0)
                    reloadTasks.Add(this.RefreshTwitterConfigurationAsync());

                await Task.WhenAll(reloadTasks);
            }

            _initial = false;

            this.timelineScheduler.Enabled = true;
        }

        private async Task doGetFollowersMenu()
        {
            await this.RefreshFollowerIdsAsync();
            this.DispSelectedPost(true);
        }

        private async void GetFollowersAllToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.doGetFollowersMenu();

        private void ReTweetUnofficialStripMenuItem_Click(object sender, EventArgs e)
            => this.doReTweetUnofficial();

        private async Task doReTweetOfficial(bool isConfirm)
        {
            //公式RT
            if (this.ExistCurrentPost)
            {
                var selectedPosts = this.CurrentTab.SelectedPosts;

                if (selectedPosts.Any(x => !x.CanRetweetBy(this.twitterApi.CurrentUserId)))
                {
                    if (selectedPosts.Any(x => x.IsProtect))
                        MessageBox.Show("Protected.");

                    _DoFavRetweetFlags = false;
                    return;
                }

                if (selectedPosts.Length > 15)
                {
                    MessageBox.Show(Properties.Resources.RetweetLimitText);
                    _DoFavRetweetFlags = false;
                    return;
                }
                else if (selectedPosts.Length > 1)
                {
                    var QuestionText = Properties.Resources.RetweetQuestion2;
                    if (_DoFavRetweetFlags) QuestionText = Properties.Resources.FavoriteRetweetQuestionText1;
                    switch (MessageBox.Show(QuestionText, "Retweet", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                    {
                        case DialogResult.Cancel:
                        case DialogResult.No:
                            _DoFavRetweetFlags = false;
                            return;
                    }
                }
                else
                {
                    if (!SettingManager.Common.RetweetNoConfirm)
                    {
                        var Questiontext = Properties.Resources.RetweetQuestion1;
                        if (_DoFavRetweetFlags) Questiontext = Properties.Resources.FavoritesRetweetQuestionText2;
                        if (isConfirm && MessageBox.Show(Questiontext, "Retweet", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                        {
                            _DoFavRetweetFlags = false;
                            return;
                        }
                    }
                }

                var statusIds = selectedPosts.Select(x => x.StatusId).ToList();

                await this.RetweetAsync(statusIds);
            }
        }

        private async void ReTweetStripMenuItem_Click(object sender, EventArgs e)
            => await this.doReTweetOfficial(true);

        private async Task FavoritesRetweetOfficial()
        {
            if (!this.ExistCurrentPost) return;
            _DoFavRetweetFlags = true;
            var retweetTask = this.doReTweetOfficial(true);
            if (_DoFavRetweetFlags)
            {
                _DoFavRetweetFlags = false;
                var favoriteTask = this.FavoriteChange(true, false);

                await Task.WhenAll(retweetTask, favoriteTask);
            }
            else
            {
                await retweetTask;
            }
        }

        private async Task FavoritesRetweetUnofficial()
        {
            var post = this.CurrentPost;
            if (this.ExistCurrentPost && post != null && !post.IsDm)
            {
                _DoFavRetweetFlags = true;
                var favoriteTask = this.FavoriteChange(true);
                if (!post.IsProtect && _DoFavRetweetFlags)
                {
                    _DoFavRetweetFlags = false;
                    doReTweetUnofficial();
                }

                await favoriteTask;
            }
        }

        /// <summary>
        /// TweetFormatterクラスによって整形された状態のHTMLを、非公式RT用に元のツイートに復元します
        /// </summary>
        /// <param name="statusHtml">TweetFormatterによって整形された状態のHTML</param>
        /// <param name="multiline">trueであればBRタグを改行に、falseであればスペースに変換します</param>
        /// <returns>復元されたツイート本文</returns>
        internal static string CreateRetweetUnofficial(string statusHtml, bool multiline)
        {
            // TweetFormatterクラスによって整形された状態のHTMLを元のツイートに復元します

            // 通常の URL
            statusHtml = Regex.Replace(statusHtml, "<a href=\"(?<href>.+?)\" title=\"(?<title>.+?)\">(?<text>.+?)</a>", "${title}");
            // メンション
            statusHtml = Regex.Replace(statusHtml, "<a class=\"mention\" href=\"(?<href>.+?)\">(?<text>.+?)</a>", "${text}");
            // ハッシュタグ
            statusHtml = Regex.Replace(statusHtml, "<a class=\"hashtag\" href=\"(?<href>.+?)\">(?<text>.+?)</a>", "${text}");
            // 絵文字
            statusHtml = Regex.Replace(statusHtml, "<img class=\"emoji\" src=\".+?\" alt=\"(?<text>.+?)\" />", "${text}");

            // <br> 除去
            if (multiline)
                statusHtml = statusHtml.Replace("<br>", Environment.NewLine);
            else
                statusHtml = statusHtml.Replace("<br>", " ");

            // &nbsp; は本来であれば U+00A0 (NON-BREAK SPACE) に置換すべきですが、
            // 現状では半角スペースの代用として &nbsp; を使用しているため U+0020 に置換します
            statusHtml = statusHtml.Replace("&nbsp;", " ");

            return WebUtility.HtmlDecode(statusHtml);
        }

        private void DumpPostClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.tweetDetailsView.DumpPostClass = this.DumpPostClassToolStripMenuItem.Checked;

            if (this.CurrentPost != null)
                this.DispSelectedPost(true);
        }

        private void MenuItemHelp_DropDownOpening(object sender, EventArgs e)
        {
            if (MyCommon.DebugBuild || MyCommon.IsKeyDown(Keys.CapsLock, Keys.Control, Keys.Shift))
                DebugModeToolStripMenuItem.Visible = true;
            else
                DebugModeToolStripMenuItem.Visible = false;
        }

        private void UrlMultibyteSplitMenuItem_CheckedChanged(object sender, EventArgs e)
            => this.urlMultibyteSplit = ((ToolStripMenuItem)sender).Checked;

        private void PreventSmsCommandMenuItem_CheckedChanged(object sender, EventArgs e)
            => this.preventSmsCommand = ((ToolStripMenuItem)sender).Checked;

        private void UrlAutoShortenMenuItem_CheckedChanged(object sender, EventArgs e)
            => SettingManager.Common.UrlConvertAuto = ((ToolStripMenuItem)sender).Checked;

        private void IdeographicSpaceToSpaceMenuItem_Click(object sender, EventArgs e)
        {
            SettingManager.Common.WideSpaceConvert = ((ToolStripMenuItem)sender).Checked;
            this.MarkSettingCommonModified();
        }

        private void FocusLockMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            SettingManager.Common.FocusLockToStatusText = ((ToolStripMenuItem)sender).Checked;
            this.MarkSettingCommonModified();
        }

        private void PostModeMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UrlMultibyteSplitMenuItem.Checked = this.urlMultibyteSplit;
            PreventSmsCommandMenuItem.Checked = this.preventSmsCommand;
            UrlAutoShortenMenuItem.Checked = SettingManager.Common.UrlConvertAuto;
            IdeographicSpaceToSpaceMenuItem.Checked = SettingManager.Common.WideSpaceConvert;
            MultiLineMenuItem.Checked = SettingManager.Local.StatusMultiline;
            FocusLockMenuItem.Checked = SettingManager.Common.FocusLockToStatusText;
        }

        private void ContextMenuPostMode_Opening(object sender, CancelEventArgs e)
        {
            UrlMultibyteSplitPullDownMenuItem.Checked = this.urlMultibyteSplit;
            PreventSmsCommandPullDownMenuItem.Checked = this.preventSmsCommand;
            UrlAutoShortenPullDownMenuItem.Checked = SettingManager.Common.UrlConvertAuto;
            IdeographicSpaceToSpacePullDownMenuItem.Checked = SettingManager.Common.WideSpaceConvert;
            MultiLinePullDownMenuItem.Checked = SettingManager.Local.StatusMultiline;
            FocusLockPullDownMenuItem.Checked = SettingManager.Common.FocusLockToStatusText;
        }

        private void TraceOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TraceOutToolStripMenuItem.Checked)
                MyCommon.TraceFlag = true;
            else
                MyCommon.TraceFlag = false;
        }

        private void TweenMain_Deactivate(object sender, EventArgs e)
            => this.StatusText_Leave(StatusText, EventArgs.Empty); // 画面が非アクティブになったら、発言欄の背景色をデフォルトへ

        private void TabRenameMenuItem_Click(object sender, EventArgs e)
        {
            if (MyCommon.IsNullOrEmpty(_rclickTabName)) return;

            _ = TabRename(_rclickTabName, out _);
        }

        private async void BitlyToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.UrlConvertAsync(MyCommon.UrlConverter.Bitly);

        private async void JmpToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.UrlConvertAsync(MyCommon.UrlConverter.Jmp);

        private async void ApiUsageInfoMenuItem_Click(object sender, EventArgs e)
        {
            TwitterApiStatus? apiStatus;

            using (var dialog = new WaitingDialog(Properties.Resources.ApiInfo6))
            {
                var cancellationToken = dialog.EnableCancellation();

                try
                {
                    var task = this.tw.GetInfoApi();
                    apiStatus = await dialog.WaitForAsync(this, task);
                }
                catch (WebApiException)
                {
                    apiStatus = null;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (apiStatus == null)
                {
                    MessageBox.Show(Properties.Resources.ApiInfo5, Properties.Resources.ApiInfo4, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            using var apiDlg = new ApiInfoDialog();
            apiDlg.ShowDialog(this);
        }

        private async void FollowCommandMenuItem_Click(object sender, EventArgs e)
        {
            var id = this.CurrentPost?.ScreenName ?? "";

            await this.FollowCommand(id);
        }

        internal async Task FollowCommand(string id)
        {
            using (var inputName = new InputTabName())
            {
                inputName.FormTitle = "Follow";
                inputName.FormDescription = Properties.Resources.FRMessage1;
                inputName.TabName = id;

                if (inputName.ShowDialog(this) != DialogResult.OK)
                    return;
                if (string.IsNullOrWhiteSpace(inputName.TabName))
                    return;

                id = inputName.TabName.Trim();
            }

            using (var dialog = new WaitingDialog(Properties.Resources.FollowCommandText1))
            {
                try
                {
                    var task = this.twitterApi.FriendshipsCreate(id).IgnoreResponse();
                    await dialog.WaitForAsync(this, task);
                }
                catch (WebApiException ex)
                {
                    MessageBox.Show(Properties.Resources.FRMessage2 + ex.Message);
                    return;
                }
            }

            MessageBox.Show(Properties.Resources.FRMessage3);
        }

        private async void RemoveCommandMenuItem_Click(object sender, EventArgs e)
        {
            var id = this.CurrentPost?.ScreenName ?? "";

            await this.RemoveCommand(id, false);
        }

        internal async Task RemoveCommand(string id, bool skipInput)
        {
            if (!skipInput)
            {
                using var inputName = new InputTabName();
                inputName.FormTitle = "Unfollow";
                inputName.FormDescription = Properties.Resources.FRMessage1;
                inputName.TabName = id;

                if (inputName.ShowDialog(this) != DialogResult.OK)
                    return;
                if (string.IsNullOrWhiteSpace(inputName.TabName))
                    return;

                id = inputName.TabName.Trim();
            }

            using (var dialog = new WaitingDialog(Properties.Resources.RemoveCommandText1))
            {
                try
                {
                    var task = this.twitterApi.FriendshipsDestroy(id).IgnoreResponse();
                    await dialog.WaitForAsync(this, task);
                }
                catch (WebApiException ex)
                {
                    MessageBox.Show(Properties.Resources.FRMessage2 + ex.Message);
                    return;
                }
            }

            MessageBox.Show(Properties.Resources.FRMessage3);
        }

        private async void FriendshipMenuItem_Click(object sender, EventArgs e)
        {
            var id = this.CurrentPost?.ScreenName ?? "";

            await this.ShowFriendship(id);
        }

        internal async Task ShowFriendship(string id)
        {
            using (var inputName = new InputTabName())
            {
                inputName.FormTitle = "Show Friendships";
                inputName.FormDescription = Properties.Resources.FRMessage1;
                inputName.TabName = id;

                if (inputName.ShowDialog(this) != DialogResult.OK)
                    return;
                if (string.IsNullOrWhiteSpace(inputName.TabName))
                    return;

                id = inputName.TabName.Trim();
            }

            bool isFollowing, isFollowed;

            using (var dialog = new WaitingDialog(Properties.Resources.ShowFriendshipText1))
            {
                var cancellationToken = dialog.EnableCancellation();

                try
                {
                    var task = this.twitterApi.FriendshipsShow(this.twitterApi.CurrentScreenName, id);
                    var friendship = await dialog.WaitForAsync(this, task);

                    isFollowing = friendship.Relationship.Source.Following;
                    isFollowed = friendship.Relationship.Source.FollowedBy;
                }
                catch (WebApiException ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        MessageBox.Show($"Err:{ex.Message}(FriendshipsShow)");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;
            }

            string result;
            if (isFollowing)
            {
                result = Properties.Resources.GetFriendshipInfo1 + System.Environment.NewLine;
            }
            else
            {
                result = Properties.Resources.GetFriendshipInfo2 + System.Environment.NewLine;
            }
            if (isFollowed)
            {
                result += Properties.Resources.GetFriendshipInfo3;
            }
            else
            {
                result += Properties.Resources.GetFriendshipInfo4;
            }
            result = id + Properties.Resources.GetFriendshipInfo5 + System.Environment.NewLine + result;
            MessageBox.Show(result);
        }

        internal async Task ShowFriendship(string[] ids)
        {
            foreach (var id in ids)
            {
                bool isFollowing, isFollowed;

                using (var dialog = new WaitingDialog(Properties.Resources.ShowFriendshipText1))
                {
                    var cancellationToken = dialog.EnableCancellation();

                    try
                    {
                        var task = this.twitterApi.FriendshipsShow(this.twitterApi.CurrentScreenName, id);
                        var friendship = await dialog.WaitForAsync(this, task);

                        isFollowing = friendship.Relationship.Source.Following;
                        isFollowed = friendship.Relationship.Source.FollowedBy;
                    }
                    catch (WebApiException ex)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            MessageBox.Show($"Err:{ex.Message}(FriendshipsShow)");
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                        return;
                }

                var result = "";
                var ff = "";

                ff = "  ";
                if (isFollowing)
                {
                    ff += Properties.Resources.GetFriendshipInfo1;
                }
                else
                {
                    ff += Properties.Resources.GetFriendshipInfo2;
                }

                ff += System.Environment.NewLine + "  ";
                if (isFollowed)
                {
                    ff += Properties.Resources.GetFriendshipInfo3;
                }
                else
                {
                    ff += Properties.Resources.GetFriendshipInfo4;
                }
                result += id + Properties.Resources.GetFriendshipInfo5 + System.Environment.NewLine + ff;
                if (isFollowing)
                {
                    if (MessageBox.Show(
                        Properties.Resources.GetFriendshipInfo7 + System.Environment.NewLine + result, Properties.Resources.GetFriendshipInfo8,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        await this.RemoveCommand(id, true);
                    }
                }
                else
                {
                    MessageBox.Show(result);
                }
            }
        }

        private async void OwnStatusMenuItem_Click(object sender, EventArgs e)
            => await this.doShowUserStatus(tw.Username, false);

        // TwitterIDでない固定文字列を調べる（文字列検証のみ　実際に取得はしない）
        // URLから切り出した文字列を渡す

        public bool IsTwitterId(string name)
        {
            if (this.tw.Configuration.NonUsernamePaths == null || this.tw.Configuration.NonUsernamePaths.Length == 0)
                return !Regex.Match(name, @"^(about|jobs|tos|privacy|who_to_follow|download|messages)$", RegexOptions.IgnoreCase).Success;
            else
                return !this.tw.Configuration.NonUsernamePaths.Contains(name, StringComparer.InvariantCultureIgnoreCase);
        }

        private void doQuoteOfficial()
        {
            var post = this.CurrentPost;
            if (this.ExistCurrentPost && post != null)
            {
                if (post.IsDm || !StatusText.Enabled)
                    return;

                if (post.IsProtect)
                {
                    MessageBox.Show("Protected.");
                    return;
                }

                var selection = (this.StatusText.SelectionStart, this.StatusText.SelectionLength);

                this.inReplyTo = null;

                StatusText.Text += " " + MyCommon.GetStatusUrl(post);

                (this.StatusText.SelectionStart, this.StatusText.SelectionLength) = selection;
                StatusText.Focus();
            }
        }

        private void doReTweetUnofficial()
        {
            //RT @id:内容
            var post = this.CurrentPost;
            if (this.ExistCurrentPost && post != null)
            {
                if (post.IsDm || !StatusText.Enabled)
                    return;

                if (post.IsProtect)
                {
                    MessageBox.Show("Protected.");
                    return;
                }
                var rtdata = post.Text;
                rtdata = CreateRetweetUnofficial(rtdata, this.StatusText.Multiline);

                var selection = (this.StatusText.SelectionStart, this.StatusText.SelectionLength);

                // 投稿時に in_reply_to_status_id を付加する
                var inReplyToStatusId = post.RetweetedId ?? post.StatusId;
                var inReplyToScreenName = post.ScreenName;
                this.inReplyTo = (inReplyToStatusId, inReplyToScreenName);

                StatusText.Text += " RT @" + post.ScreenName + ": " + rtdata;

                (this.StatusText.SelectionStart, this.StatusText.SelectionLength) = selection;
                StatusText.Focus();
            }
        }

        private void QuoteStripMenuItem_Click(object sender, EventArgs e)
            => this.doQuoteOfficial();

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            //公式検索
            var pnl = ((Control)sender).Parent;
            if (pnl == null) return;
            var tbName = pnl.Parent.Text;
            var tb = (PublicSearchTabModel)_statuses.Tabs[tbName];
            var cmb = (ComboBox)pnl.Controls["comboSearch"];
            var cmbLang = (ComboBox)pnl.Controls["comboLang"];
            cmb.Text = cmb.Text.Trim();
            // 検索式演算子 OR についてのみ大文字しか認識しないので強制的に大文字とする
            var Quote = false;
            var buf = new StringBuilder();
            var c = cmb.Text.ToCharArray();
            for (var cnt = 0; cnt < cmb.Text.Length; cnt++)
            {
                if (cnt > cmb.Text.Length - 4)
                {
                    buf.Append(cmb.Text.Substring(cnt));
                    break;
                }
                if (c[cnt] == '"')
                {
                    Quote = !Quote;
                }
                else
                {
                    if (!Quote && cmb.Text.Substring(cnt, 4).Equals(" or ", StringComparison.OrdinalIgnoreCase))
                    {
                        buf.Append(" OR ");
                        cnt += 3;
                        continue;
                    }
                }
                buf.Append(c[cnt]);
            }
            cmb.Text = buf.ToString();

            var listView = (DetailsListView)pnl.Parent.Tag;

            var queryChanged = tb.SearchWords != cmb.Text || tb.SearchLang != cmbLang.Text;

            tb.SearchWords = cmb.Text;
            tb.SearchLang = cmbLang.Text;
            if (MyCommon.IsNullOrEmpty(cmb.Text))
            {
                listView.Focus();
                SaveConfigsTabs();
                return;
            }
            if (queryChanged)
            {
                var idx = cmb.Items.IndexOf(tb.SearchWords);
                if (idx > -1) cmb.Items.RemoveAt(idx);
                cmb.Items.Insert(0, tb.SearchWords);
                cmb.Text = tb.SearchWords;
                cmb.SelectAll();
                this.PurgeListViewItemCache();
                listView.VirtualListSize = 0;
                _statuses.ClearTabIds(tbName);
                SaveConfigsTabs();   //検索条件の保存
            }

            listView.Focus();
            await this.RefreshTabAsync(tb);
        }

        private async void RefreshMoreStripMenuItem_Click(object sender, EventArgs e)
            => await this.DoRefreshMore(); // もっと前を取得

        /// <summary>
        /// 指定されたタブのListTabにおける位置を返します
        /// </summary>
        /// <remarks>
        /// 非表示のタブについて -1 が返ることを常に考慮して下さい
        /// </remarks>
        public int GetTabPageIndex(string tabName)
            => this._statuses.Tabs.IndexOf(tabName);

        private void UndoRemoveTabMenuItem_Click(object sender, EventArgs e)
        {
            if (_statuses.RemovedTab.Count == 0)
            {
                MessageBox.Show("There isn't removed tab.", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            else
            {
                DetailsListView? listView;

                var tb = _statuses.RemovedTab.Pop();
                if (tb.TabType == MyCommon.TabUsageType.Related)
                {
                    var relatedTab = _statuses.GetTabByType(MyCommon.TabUsageType.Related);
                    if (relatedTab != null)
                    {
                        // 関連発言なら既存のタブを置き換える
                        tb.TabName = relatedTab.TabName;
                        this.ClearTab(tb.TabName, false);

                        this._statuses.ReplaceTab(tb);

                        var tabIndex = this._statuses.Tabs.IndexOf(tb);
                        var tabPage = this.ListTab.TabPages[tabIndex];
                        listView = (DetailsListView)tabPage.Tag;
                        this.ListTab.SelectedIndex = tabIndex;
                    }
                    else
                    {
                        const string TabName = "Related Tweets";
                        var renamed = TabName;
                        for (var i = 2; i <= 100; i++)
                        {
                            if (!_statuses.ContainsTab(renamed))
                                break;
                            renamed = TabName + i;
                        }
                        tb.TabName = renamed;

                        _statuses.AddTab(tb);
                        AddNewTab(tb, startup: false);

                        var tabIndex = this._statuses.Tabs.Count - 1;
                        var tabPage = this.ListTab.TabPages[tabIndex];

                        listView = (DetailsListView)tabPage.Tag;
                        this.ListTab.SelectedIndex = tabIndex;
                    }
                }
                else
                {
                    var renamed = tb.TabName;
                    for (var i = 1; i < int.MaxValue; i++)
                    {
                        if (!_statuses.ContainsTab(renamed))
                            break;
                        renamed = tb.TabName + "(" + i + ")";
                    }
                    tb.TabName = renamed;

                    _statuses.AddTab(tb);
                    AddNewTab(tb, startup: false);

                    var tabIndex = this._statuses.Tabs.Count - 1;
                    var tabPage = this.ListTab.TabPages[tabIndex];

                    listView = (DetailsListView)tabPage.Tag;
                    this.ListTab.SelectedIndex = tabIndex;
                }
                SaveConfigsTabs();

                if (listView != null)
                {
                    using (ControlTransaction.Update(listView))
                    {
                        listView.VirtualListSize = tb.AllCount;
                    }
                }
            }
        }

        private async Task doMoveToRTHome()
        {
            var post = this.CurrentPost;
            if (post != null && post.RetweetedId != null)
                await this.OpenUriInBrowserAsync("https://twitter.com/" + post.RetweetedBy);
        }

        private async void MoveToRTHomeMenuItem_Click(object sender, EventArgs e)
            => await this.doMoveToRTHome();

        private void ListManageUserContextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var screenName = this.CurrentPost?.ScreenName;
            if (screenName != null)
                this.ListManageUserContext(screenName);
        }

        public void ListManageUserContext(string screenName)
        {
            using var listSelectForm = new MyLists(screenName, this.twitterApi);
            listSelectForm.ShowDialog(this);
        }

        private void SearchControls_Enter(object sender, EventArgs e)
        {
            var pnl = (Control)sender;
            foreach (Control ctl in pnl.Controls)
            {
                ctl.TabStop = true;
            }
        }

        private void SearchControls_Leave(object sender, EventArgs e)
        {
            var pnl = (Control)sender;
            foreach (Control ctl in pnl.Controls)
            {
                ctl.TabStop = false;
            }
        }

        private void PublicSearchQueryMenuItem_Click(object sender, EventArgs e)
        {
            var tab = this.CurrentTab;
            if (tab.TabType != MyCommon.TabUsageType.PublicSearch) return;
            this.CurrentTabPage.Controls["panelSearch"].Controls["comboSearch"].Focus();
        }

        private void StatusLabel_DoubleClick(object sender, EventArgs e)
            => MessageBox.Show(StatusLabel.TextHistory, "Logs", MessageBoxButtons.OK, MessageBoxIcon.None);

        private void HashManageMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult rslt;
            try
            {
                rslt = HashMgr.ShowDialog();
            }
            catch (Exception)
            {
                return;
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
            if (rslt == DialogResult.Cancel) return;
            if (!MyCommon.IsNullOrEmpty(HashMgr.UseHash))
            {
                HashStripSplitButton.Text = HashMgr.UseHash;
                HashTogglePullDownMenuItem.Checked = true;
                HashToggleMenuItem.Checked = true;
            }
            else
            {
                HashStripSplitButton.Text = "#[-]";
                HashTogglePullDownMenuItem.Checked = false;
                HashToggleMenuItem.Checked = false;
            }

            this.MarkSettingCommonModified();
            this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);
        }

        private void HashToggleMenuItem_Click(object sender, EventArgs e)
        {
            HashMgr.ToggleHash();
            if (!MyCommon.IsNullOrEmpty(HashMgr.UseHash))
            {
                HashStripSplitButton.Text = HashMgr.UseHash;
                HashToggleMenuItem.Checked = true;
                HashTogglePullDownMenuItem.Checked = true;
            }
            else
            {
                HashStripSplitButton.Text = "#[-]";
                HashToggleMenuItem.Checked = false;
                HashTogglePullDownMenuItem.Checked = false;
            }
            this.MarkSettingCommonModified();
            this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);
        }

        private void HashStripSplitButton_ButtonClick(object sender, EventArgs e)
            => this.HashToggleMenuItem_Click(this.HashToggleMenuItem, EventArgs.Empty);

        public void SetPermanentHashtag(string hashtag)
        {
            HashMgr.SetPermanentHash("#" + hashtag);
            HashStripSplitButton.Text = HashMgr.UseHash;
            HashTogglePullDownMenuItem.Checked = true;
            HashToggleMenuItem.Checked = true;
            //使用ハッシュタグとして設定
            this.MarkSettingCommonModified();
        }

        private void MenuItemOperate_DropDownOpening(object sender, EventArgs e)
        {
            if (!this.ExistCurrentPost)
            {
                this.ReplyOpMenuItem.Enabled = false;
                this.ReplyAllOpMenuItem.Enabled = false;
                this.DmOpMenuItem.Enabled = false;
                this.ShowProfMenuItem.Enabled = false;
                this.ShowUserTimelineToolStripMenuItem.Enabled = false;
                this.ListManageMenuItem.Enabled = false;
                this.OpenFavOpMenuItem.Enabled = false;
                this.CreateTabRuleOpMenuItem.Enabled = false;
                this.CreateIdRuleOpMenuItem.Enabled = false;
                this.CreateSourceRuleOpMenuItem.Enabled = false;
                this.ReadOpMenuItem.Enabled = false;
                this.UnreadOpMenuItem.Enabled = false;
            }
            else
            {
                this.ReplyOpMenuItem.Enabled = true;
                this.ReplyAllOpMenuItem.Enabled = true;
                this.DmOpMenuItem.Enabled = true;
                this.ShowProfMenuItem.Enabled = true;
                this.ShowUserTimelineToolStripMenuItem.Enabled = true;
                this.ListManageMenuItem.Enabled = true;
                this.OpenFavOpMenuItem.Enabled = true;
                this.CreateTabRuleOpMenuItem.Enabled = true;
                this.CreateIdRuleOpMenuItem.Enabled = true;
                this.CreateSourceRuleOpMenuItem.Enabled = true;
                this.ReadOpMenuItem.Enabled = true;
                this.UnreadOpMenuItem.Enabled = true;
            }

            var tab = this.CurrentTab;
            var post = this.CurrentPost;
            if (tab.TabType == MyCommon.TabUsageType.DirectMessage || !this.ExistCurrentPost || post == null || post.IsDm)
            {
                this.FavOpMenuItem.Enabled = false;
                this.UnFavOpMenuItem.Enabled = false;
                this.OpenStatusOpMenuItem.Enabled = false;
                this.ShowRelatedStatusesMenuItem2.Enabled = false;
                this.RtOpMenuItem.Enabled = false;
                this.RtUnOpMenuItem.Enabled = false;
                this.QtOpMenuItem.Enabled = false;
                this.FavoriteRetweetMenuItem.Enabled = false;
                this.FavoriteRetweetUnofficialMenuItem.Enabled = false;
            }
            else
            {
                this.FavOpMenuItem.Enabled = true;
                this.UnFavOpMenuItem.Enabled = true;
                this.OpenStatusOpMenuItem.Enabled = true;
                this.ShowRelatedStatusesMenuItem2.Enabled = true;  //PublicSearchの時問題出るかも

                if (!post.CanRetweetBy(this.twitterApi.CurrentUserId))
                {
                    this.RtOpMenuItem.Enabled = false;
                    this.RtUnOpMenuItem.Enabled = false;
                    this.QtOpMenuItem.Enabled = false;
                    this.FavoriteRetweetMenuItem.Enabled = false;
                    this.FavoriteRetweetUnofficialMenuItem.Enabled = false;
                }
                else
                {
                    this.RtOpMenuItem.Enabled = true;
                    this.RtUnOpMenuItem.Enabled = true;
                    this.QtOpMenuItem.Enabled = true;
                    this.FavoriteRetweetMenuItem.Enabled = true;
                    this.FavoriteRetweetUnofficialMenuItem.Enabled = true;
                }
            }

            if (tab.TabType != MyCommon.TabUsageType.Favorites)
            {
                this.RefreshPrevOpMenuItem.Enabled = true;
            }
            else
            {
                this.RefreshPrevOpMenuItem.Enabled = false;
            }
            if (!this.ExistCurrentPost || post == null || post.InReplyToStatusId == null)
            {
                OpenRepSourceOpMenuItem.Enabled = false;
            }
            else
            {
                OpenRepSourceOpMenuItem.Enabled = true;
            }
            if (!this.ExistCurrentPost || post == null || MyCommon.IsNullOrEmpty(post.RetweetedBy))
            {
                OpenRterHomeMenuItem.Enabled = false;
            }
            else
            {
                OpenRterHomeMenuItem.Enabled = true;
            }

            if (this.ExistCurrentPost && post != null)
            {
                this.DelOpMenuItem.Enabled = post.CanDeleteBy(this.tw.UserId);
            }
        }

        private void MenuItemTab_DropDownOpening(object sender, EventArgs e)
            => this.ContextMenuTabProperty_Opening(sender, null!);

        public Twitter TwitterInstance
            => this.tw;

        private void SplitContainer3_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (this._initialLayout)
                return;

            int splitterDistance;
            switch (this.WindowState)
            {
                case FormWindowState.Normal:
                    splitterDistance = this.SplitContainer3.SplitterDistance;
                    break;
                case FormWindowState.Maximized:
                    // 最大化時は、通常時のウィンドウサイズに換算した SplitterDistance を算出する
                    var normalContainerWidth = this._mySize.Width - SystemInformation.Border3DSize.Width * 2;
                    splitterDistance = this.SplitContainer3.SplitterDistance - (this.SplitContainer3.Width - normalContainerWidth);
                    splitterDistance = Math.Min(splitterDistance, normalContainerWidth - this.SplitContainer3.SplitterWidth - this.SplitContainer3.Panel2MinSize);
                    break;
                default:
                    return;
            }

            this._mySpDis3 = splitterDistance;
            this.MarkSettingLocalModified();
        }

        private void MenuItemEdit_DropDownOpening(object sender, EventArgs e)
        {
            if (_statuses.RemovedTab.Count == 0)
            {
                UndoRemoveTabMenuItem.Enabled = false;
            }
            else
            {
                UndoRemoveTabMenuItem.Enabled = true;
            }

            if (this.CurrentTab.TabType == MyCommon.TabUsageType.PublicSearch)
                PublicSearchQueryMenuItem.Enabled = true;
            else
                PublicSearchQueryMenuItem.Enabled = false;

            var post = this.CurrentPost;
            if (!this.ExistCurrentPost || post == null)
            {
                this.CopySTOTMenuItem.Enabled = false;
                this.CopyURLMenuItem.Enabled = false;
                this.CopyUserIdStripMenuItem.Enabled = false;
            }
            else
            {
                this.CopySTOTMenuItem.Enabled = true;
                this.CopyURLMenuItem.Enabled = true;
                this.CopyUserIdStripMenuItem.Enabled = true;

                if (post.IsDm) this.CopyURLMenuItem.Enabled = false;
                if (post.IsProtect) this.CopySTOTMenuItem.Enabled = false;
            }
        }

        private void NotifyIcon1_MouseMove(object sender, MouseEventArgs e)
            => this.SetNotifyIconText();

        private async void UserStatusToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.ShowUserStatus(this.CurrentPost?.ScreenName ?? "");

        private async Task doShowUserStatus(string id, bool ShowInputDialog)
        {
            TwitterUser? user = null;

            if (ShowInputDialog)
            {
                using var inputName = new InputTabName();
                inputName.FormTitle = "Show UserStatus";
                inputName.FormDescription = Properties.Resources.FRMessage1;
                inputName.TabName = id;

                if (inputName.ShowDialog(this) != DialogResult.OK)
                    return;
                if (string.IsNullOrWhiteSpace(inputName.TabName))
                    return;

                id = inputName.TabName.Trim();
            }

            using (var dialog = new WaitingDialog(Properties.Resources.doShowUserStatusText1))
            {
                var cancellationToken = dialog.EnableCancellation();

                try
                {
                    var task = this.twitterApi.UsersShow(id);
                    user = await dialog.WaitForAsync(this, task);
                }
                catch (WebApiException ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        MessageBox.Show($"Err:{ex.Message}(UsersShow)");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;
            }

            await this.doShowUserStatus(user);
        }

        private async Task doShowUserStatus(TwitterUser user)
        {
            using var userDialog = new UserInfoDialog(this, this.twitterApi);
            var showUserTask = userDialog.ShowUserAsync(user);
            userDialog.ShowDialog(this);

            this.Activate();
            this.BringToFront();

            // ユーザー情報の表示が完了するまで userDialog を破棄しない
            await showUserTask;
        }

        internal Task ShowUserStatus(string id, bool ShowInputDialog)
            => this.doShowUserStatus(id, ShowInputDialog);

        internal Task ShowUserStatus(string id)
            => this.doShowUserStatus(id, true);

        private async void ShowProfileMenuItem_Click(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (post != null)
            {
                await this.ShowUserStatus(post.ScreenName, false);
            }
        }

        private async void RtCountMenuItem_Click(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (!this.ExistCurrentPost || post == null)
                return;

            var statusId = post.RetweetedId ?? post.StatusId;
            TwitterStatus status;

            using (var dialog = new WaitingDialog(Properties.Resources.RtCountMenuItem_ClickText1))
            {
                var cancellationToken = dialog.EnableCancellation();

                try
                {
                    var task = this.twitterApi.StatusesShow(statusId);
                    status = await dialog.WaitForAsync(this, task);
                }
                catch (WebApiException ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        MessageBox.Show(Properties.Resources.RtCountText2 + Environment.NewLine + "Err:" + ex.Message);
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;
            }

            MessageBox.Show(status.RetweetCount + Properties.Resources.RtCountText1);
        }

        private readonly HookGlobalHotkey _hookGlobalHotkey;
        public TweenMain()
        {
            _hookGlobalHotkey = new HookGlobalHotkey(this);

            // この呼び出しは、Windows フォーム デザイナで必要です。
            InitializeComponent();

            // InitializeComponent() 呼び出しの後で初期化を追加します。

            if (!this.DesignMode)
            {
                // デザイナでの編集時にレイアウトが縦方向に数pxずれる問題の対策
                this.StatusText.Dock = DockStyle.Fill;
            }

            this.tweetDetailsView.Owner = this;

            this._hookGlobalHotkey.HotkeyPressed += _hookGlobalHotkey_HotkeyPressed;
            this.gh.NotifyClicked += GrowlHelper_Callback;

            // メイリオフォント指定時にタブの最小幅が広くなる問題の対策
            this.ListTab.HandleCreated += (s, e) => NativeMethods.SetMinTabWidth((TabControl)s, 40);

            this.ImageSelector.Visible = false;
            this.ImageSelector.Enabled = false;
            this.ImageSelector.FilePickDialog = OpenFileDialog1;

            this.workerProgress = new Progress<string>(x => this.StatusLabel.Text = x);

            this.ReplaceAppName();
            this.InitializeShortcuts();
        }

        private void _hookGlobalHotkey_HotkeyPressed(object sender, KeyEventArgs e)
        {
            if ((this.WindowState == FormWindowState.Normal || this.WindowState == FormWindowState.Maximized) && this.Visible && Form.ActiveForm == this)
            {
                //アイコン化
                this.Visible = false;
            }
            else if (Form.ActiveForm == null)
            {
                this.Visible = true;
                if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
                this.Activate();
                this.BringToFront();
                this.StatusText.Focus();
            }
        }

        private void SplitContainer2_MouseDoubleClick(object sender, MouseEventArgs e)
            => this.MultiLinePullDownMenuItem.PerformClick();

#region "画像投稿"
        private void ImageSelectMenuItem_Click(object sender, EventArgs e)
        {
            if (ImageSelector.Visible)
                ImageSelector.EndSelection();
            else
                ImageSelector.BeginSelection();
        }

        private void SelectMedia_DragEnter(DragEventArgs e)
        {
            if (ImageSelector.HasUploadableService(((string[])e.Data.GetData(DataFormats.FileDrop, false))[0], true))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
            e.Effect = DragDropEffects.None;
        }

        private void SelectMedia_DragDrop(DragEventArgs e)
        {
            this.Activate();
            this.BringToFront();
            ImageSelector.BeginSelection((string[])e.Data.GetData(DataFormats.FileDrop, false));
            StatusText.Focus();
        }

        private void ImageSelector_BeginSelecting(object sender, EventArgs e)
        {
            TimelinePanel.Visible = false;
            TimelinePanel.Enabled = false;
        }

        private void ImageSelector_EndSelecting(object sender, EventArgs e)
        {
            TimelinePanel.Visible = true;
            TimelinePanel.Enabled = true;
            this.CurrentListView.Focus();
        }

        private void ImageSelector_FilePickDialogOpening(object sender, EventArgs e)
            => this.AllowDrop = false;

        private void ImageSelector_FilePickDialogClosed(object sender, EventArgs e)
            => this.AllowDrop = true;

        private void ImageSelector_SelectedServiceChanged(object sender, EventArgs e)
        {
            if (ImageSelector.Visible)
            {
                this.MarkSettingCommonModified();
                this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);
            }
        }

        private void ImageSelector_VisibleChanged(object sender, EventArgs e)
            => this.StatusText_TextChanged(this.StatusText, EventArgs.Empty);

        /// <summary>
        /// StatusTextでCtrl+Vが押下された時の処理
        /// </summary>
        private void ProcClipboardFromStatusTextWhenCtrlPlusV()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    // clipboardにテキストがある場合は貼り付け処理
                    this.StatusText.Paste(Clipboard.GetText());
                }
                else if (Clipboard.ContainsImage())
                {
                    // 画像があるので投稿処理を行う
                    if (MessageBox.Show(Properties.Resources.PostPictureConfirm3,
                                       Properties.Resources.PostPictureWarn4,
                                       MessageBoxButtons.OKCancel,
                                       MessageBoxIcon.Question,
                                       MessageBoxDefaultButton.Button2)
                                   == DialogResult.OK)
                    {
                        // clipboardから画像を取得
                        using var image = Clipboard.GetImage();
                        this.ImageSelector.BeginSelection(image);
                    }
                }
            }
            catch (ExternalException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
#endregion

        private void ListManageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var form = new ListManage(tw);
            form.ShowDialog(this);
        }

        private bool ModifySettingCommon { get; set; }
        private bool ModifySettingLocal { get; set; }
        private bool ModifySettingAtId { get; set; }

        private void MenuItemCommand_DropDownOpening(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (this.ExistCurrentPost && post != null && !post.IsDm)
                RtCountMenuItem.Enabled = true;
            else
                RtCountMenuItem.Enabled = false;
        }

        private void CopyUserIdStripMenuItem_Click(object sender, EventArgs e)
            => this.CopyUserId();

        private void CopyUserId()
        {
            var post = this.CurrentPost;
            if (post == null) return;
            var clstr = post.ScreenName;
            try
            {
                Clipboard.SetDataObject(clstr, false, 5, 100);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void ShowRelatedStatusesMenuItem_Click(object sender, EventArgs e)
        {
            var post = this.CurrentPost;
            if (this.ExistCurrentPost && post != null && !post.IsDm)
            {
                try
                {
                    await this.OpenRelatedTab(post);
                }
                catch (TabException ex)
                {
                    MessageBox.Show(this, ex.Message, ApplicationSettings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 指定されたツイートに対する関連発言タブを開きます
        /// </summary>
        /// <param name="statusId">表示するツイートのID</param>
        /// <exception cref="TabException">名前の重複が多すぎてタブを作成できない場合</exception>
        public async Task OpenRelatedTab(long statusId)
        {
            var post = this._statuses[statusId];
            if (post == null)
            {
                try
                {
                    post = await this.tw.GetStatusApi(false, statusId);
                }
                catch (WebApiException ex)
                {
                    this.StatusLabel.Text = $"Err:{ex.Message}(GetStatus)";
                    return;
                }
            }

            await this.OpenRelatedTab(post);
        }

        /// <summary>
        /// 指定されたツイートに対する関連発言タブを開きます
        /// </summary>
        /// <param name="post">表示する対象となるツイート</param>
        /// <exception cref="TabException">名前の重複が多すぎてタブを作成できない場合</exception>
        private async Task OpenRelatedTab(PostClass post)
        {
            var tabRelated = this._statuses.GetTabByType<RelatedPostsTabModel>();
            if (tabRelated != null)
            {
                this.RemoveSpecifiedTab(tabRelated.TabName, confirm: false);
            }

            var tabName = this._statuses.MakeTabName("Related Tweets");

            tabRelated = new RelatedPostsTabModel(tabName, post)
            {
                UnreadManage = false,
                Notify = false,
            };

            this._statuses.AddTab(tabRelated);
            this.AddNewTab(tabRelated, startup: false);

            this.ListTab.SelectedIndex = this._statuses.Tabs.IndexOf(tabName);

            await this.RefreshTabAsync(tabRelated);

            var tabIndex = this._statuses.Tabs.IndexOf(tabRelated.TabName);

            if (tabIndex != -1)
            {
                // TODO: 非同期更新中にタブが閉じられている場合を厳密に考慮したい

                var tabPage = this.ListTab.TabPages[tabIndex];
                var listView = (DetailsListView)tabPage.Tag;
                var targetPost = tabRelated.TargetPost;
                var index = tabRelated.IndexOf(targetPost.RetweetedId ?? targetPost.StatusId);

                if (index != -1 && index < listView.Items.Count)
                {
                    listView.SelectedIndices.Add(index);
                    listView.Items[index].Focused = true;
                }
            }
        }

        private void CacheInfoMenuItem_Click(object sender, EventArgs e)
        {
            var buf = new StringBuilder();
            buf.AppendFormat("キャッシュエントリ保持数     : {0}" + Environment.NewLine, IconCache.CacheCount);
            buf.AppendFormat("キャッシュエントリ破棄数     : {0}" + Environment.NewLine, IconCache.CacheRemoveCount);
            MessageBox.Show(buf.ToString(), "アイコンキャッシュ使用状況");
        }

#region "Userstream"
        private async void tw_PostDeleted(object sender, PostDeletedEventArgs e)
        {
            try
            {
                if (InvokeRequired && !IsDisposed)
                {
                    await this.InvokeAsync(() =>
                    {
                        this._statuses.RemovePostFromAllTabs(e.StatusId, setIsDeleted: true);
                        if (this.CurrentTab.Contains(e.StatusId))
                        {
                            this.PurgeListViewItemCache();
                            this.CurrentListView.Update();
                            var post = this.CurrentPost;
                            if (post != null && post.StatusId == e.StatusId)
                                this.DispSelectedPost(true);
                        }
                    });
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }

        private void tw_NewPostFromStream(object sender, EventArgs e)
        {
            if (SettingManager.Common.ReadOldPosts)
            {
                _statuses.SetReadHomeTab(); //新着時未読クリア
            }

            this._statuses.DistributePosts();

            this.RefreshThrottlingTimer.Call();
        }

        private async void tw_UserStreamStarted(object sender, EventArgs e)
        {
            try
            {
                if (InvokeRequired && !IsDisposed)
                {
                    await this.InvokeAsync(() => this.tw_UserStreamStarted(sender, e));
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            this.RefreshUserStreamsMenu();
            this.MenuItemUserStream.Enabled = true;

            StatusLabel.Text = "UserStream Started.";
        }

        private async void tw_UserStreamStopped(object sender, EventArgs e)
        {
            try
            {
                if (InvokeRequired && !IsDisposed)
                {
                    await this.InvokeAsync(() => this.tw_UserStreamStopped(sender, e));
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            this.RefreshUserStreamsMenu();
            this.MenuItemUserStream.Enabled = true;

            StatusLabel.Text = "UserStream Stopped.";
        }

        private void RefreshUserStreamsMenu()
        {
            if (this.tw.UserStreamActive)
            {
                this.MenuItemUserStream.Text = "&UserStream ▶";
                this.StopToolStripMenuItem.Text = "&Stop";
            }
            else
            {
                this.MenuItemUserStream.Text = "&UserStream ■";
                this.StopToolStripMenuItem.Text = "&Start";
            }
        }

        private async void tw_UserStreamEventArrived(object sender, UserStreamEventReceivedEventArgs e)
        {
            try
            {
                if (InvokeRequired && !IsDisposed)
                {
                    await this.InvokeAsync(() => this.tw_UserStreamEventArrived(sender, e));
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            var ev = e.EventData;
            StatusLabel.Text = "Event: " + ev.Event;
            NotifyEvent(ev);
            if (ev.Event == "favorite" || ev.Event == "unfavorite")
            {
                if (this.CurrentTab.Contains(ev.Id))
                {
                    this.PurgeListViewItemCache();
                    this.CurrentListView.Update();
                }
                if (ev.Event == "unfavorite" && ev.Username.Equals(tw.Username, StringComparison.InvariantCultureIgnoreCase))
                {
                    var favTab = this._statuses.FavoriteTab;
                    favTab.EnqueueRemovePost(ev.Id, setIsDeleted: false);
                }
            }
        }

        private void NotifyEvent(Twitter.FormattedEvent ev)
        {
            //新着通知 
            if (BalloonRequired(ev))
            {
                NotifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
                var title = new StringBuilder();
                if (SettingManager.Common.DispUsername)
                {
                    title.Append(tw.Username);
                    title.Append(" - ");
                }
                title.Append(ApplicationSettings.ApplicationName);
                title.Append(" [");
                title.Append(ev.Event.ToUpper(CultureInfo.CurrentCulture));
                title.Append("] by ");
                if (!MyCommon.IsNullOrEmpty(ev.Username))
                {
                    title.Append(ev.Username);
                }

                string text;
                if (!MyCommon.IsNullOrEmpty(ev.Target))
                    text = ev.Target;
                else
                    text = " ";

                if (SettingManager.Common.IsUseNotifyGrowl)
                {
                    gh.Notify(GrowlHelper.NotifyType.UserStreamEvent,
                              ev.Id.ToString(), title.ToString(), text);
                }
                else
                {
                    NotifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
                    NotifyIcon1.BalloonTipTitle = title.ToString();
                    NotifyIcon1.BalloonTipText = text;
                    NotifyIcon1.ShowBalloonTip(500);
                }
            }

            //サウンド再生
            var snd = SettingManager.Common.EventSoundFile;
            if (!_initial && SettingManager.Common.PlaySound && !MyCommon.IsNullOrEmpty(snd))
            {
                if ((ev.Eventtype & SettingManager.Common.EventNotifyFlag) != 0 && IsMyEventNotityAsEventType(ev))
                {
                    try
                    {
                        var dir = Application.StartupPath;
                        if (Directory.Exists(Path.Combine(dir, "Sounds")))
                        {
                            dir = Path.Combine(dir, "Sounds");
                        }
                        using var player = new SoundPlayer(Path.Combine(dir, snd));
                        player.Play();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private void StopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MenuItemUserStream.Enabled = false;
            if (StopRefreshAllMenuItem.Checked)
            {
                StopRefreshAllMenuItem.Checked = false;
                return;
            }
            if (this.tw.UserStreamActive)
            {
                tw.StopUserStream();
            }
            else
            {
                tw.StartUserStream();
            }
        }

        private static string inputTrack = "";

        private void TrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TrackToolStripMenuItem.Checked)
            {
                using (var inputForm = new InputTabName())
                {
                    inputForm.TabName = inputTrack;
                    inputForm.FormTitle = "Input track word";
                    inputForm.FormDescription = "Track word";
                    if (inputForm.ShowDialog() != DialogResult.OK)
                    {
                        TrackToolStripMenuItem.Checked = false;
                        return;
                    }
                    inputTrack = inputForm.TabName.Trim();
                }
                if (!inputTrack.Equals(tw.TrackWord))
                {
                    tw.TrackWord = inputTrack;
                    this.MarkSettingCommonModified();
                    TrackToolStripMenuItem.Checked = !MyCommon.IsNullOrEmpty(inputTrack);
                    tw.ReconnectUserStream();
                }
            }
            else
            {
                tw.TrackWord = "";
                tw.ReconnectUserStream();
            }
            this.MarkSettingCommonModified();
        }

        private void AllrepliesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tw.AllAtReply = AllrepliesToolStripMenuItem.Checked;
            this.MarkSettingCommonModified();
            tw.ReconnectUserStream();
        }

        private void EventViewerMenuItem_Click(object sender, EventArgs e)
        {
            if (evtDialog == null || evtDialog.IsDisposed)
            {
                this.evtDialog = new EventViewerDialog
                {
                    Owner = this,
                };

                //親の中央に表示
                this.evtDialog.Location = new Point
                {
                    X = Convert.ToInt32(this.Location.X + this.Size.Width / 2 - evtDialog.Size.Width / 2),
                    Y = Convert.ToInt32(this.Location.Y + this.Size.Height / 2 - evtDialog.Size.Height / 2),
                };
            }
            evtDialog.EventSource = tw.StoredEvent;
            if (!evtDialog.Visible)
            {
                evtDialog.Show(this);
            }
            else
            {
                evtDialog.Activate();
            }
            this.TopMost = SettingManager.Common.AlwaysTop;
        }
#endregion

        private void TweenRestartMenuItem_Click(object sender, EventArgs e)
        {
            MyCommon._endingFlag = true;
            try
            {
                this.Close();
                Application.Restart();
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to restart. Please run " + ApplicationSettings.ApplicationName + " manually.");
            }
        }

        private async void OpenOwnHomeMenuItem_Click(object sender, EventArgs e)
            => await this.OpenUriInBrowserAsync(MyCommon.TwitterUrl + tw.Username);

        private bool ExistCurrentPost
        {
            get
            {
                var post = this.CurrentPost;
                return post != null && !post.IsDeleted;
            }
        }

        private async void ShowUserTimelineToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.ShowUserTimeline();

        private string GetUserIdFromCurPostOrInput(string caption)
        {
            var id = this.CurrentPost?.ScreenName ?? "";

            using var inputName = new InputTabName();
            inputName.FormTitle = caption;
            inputName.FormDescription = Properties.Resources.FRMessage1;
            inputName.TabName = id;

            if (inputName.ShowDialog() == DialogResult.OK &&
                !MyCommon.IsNullOrEmpty(inputName.TabName.Trim()))
            {
                id = inputName.TabName.Trim();
            }
            else
            {
                id = "";
            }
            return id;
        }

        private async void UserTimelineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var id = GetUserIdFromCurPostOrInput("Show UserTimeline");
            if (!MyCommon.IsNullOrEmpty(id))
            {
                await this.AddNewTabForUserTimeline(id);
            }
        }

        private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                this.timelineScheduler.SystemResumed();
        }

        private void SystemEvents_TimeChanged(object sender, EventArgs e)
        {
            var prevTimeOffset = TimeZoneInfo.Local.BaseUtcOffset;

            TimeZoneInfo.ClearCachedData();

            var curTimeOffset = TimeZoneInfo.Local.BaseUtcOffset;

            if (curTimeOffset != prevTimeOffset)
            {
                // タイムゾーンの変更を反映
                this.PurgeListViewItemCache();
                this.CurrentListView.Refresh();

                this.DispSelectedPost(forceupdate: true);
            }
        }

        private void TimelineRefreshEnableChange(bool isEnable)
        {
            if (isEnable)
            {
                tw.StartUserStream();
            }
            else
            {
                tw.StopUserStream();
            }
            this.timelineScheduler.Enabled = isEnable;
        }

        private void StopRefreshAllMenuItem_CheckedChanged(object sender, EventArgs e)
            => this.TimelineRefreshEnableChange(!StopRefreshAllMenuItem.Checked);

        private async Task OpenUserAppointUrl()
        {
            if (SettingManager.Common.UserAppointUrl != null)
            {
                if (SettingManager.Common.UserAppointUrl.Contains("{ID}") || SettingManager.Common.UserAppointUrl.Contains("{STATUS}"))
                {
                    var post = this.CurrentPost;
                    if (post != null)
                    {
                        var xUrl = SettingManager.Common.UserAppointUrl;
                        xUrl = xUrl.Replace("{ID}", post.ScreenName);

                        var statusId = post.RetweetedId ?? post.StatusId;
                        xUrl = xUrl.Replace("{STATUS}", statusId.ToString());

                        await this.OpenUriInBrowserAsync(xUrl);
                    }
                }
                else
                {
                    await this.OpenUriInBrowserAsync(SettingManager.Common.UserAppointUrl);
                }
            }
        }

        private async void OpenUserSpecifiedUrlMenuItem_Click(object sender, EventArgs e)
            => await this.OpenUserAppointUrl();

        private async void GrowlHelper_Callback(object sender, GrowlHelper.NotifyCallbackEventArgs e)
        {
            if (Form.ActiveForm == null)
            {
                await this.InvokeAsync(() =>
                {
                    this.Visible = true;
                    if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
                    this.Activate();
                    this.BringToFront();
                    if (e.NotifyType == GrowlHelper.NotifyType.DirectMessage)
                    {
                        if (!this.GoDirectMessage(e.StatusId)) this.StatusText.Focus();
                    }
                    else
                    {
                        if (!this.GoStatus(e.StatusId)) this.StatusText.Focus();
                    }
                });
            }
        }

        private void ReplaceAppName()
        {
            MatomeMenuItem.Text = MyCommon.ReplaceAppName(MatomeMenuItem.Text);
            AboutMenuItem.Text = MyCommon.ReplaceAppName(AboutMenuItem.Text);
        }

        private void tweetThumbnail1_ThumbnailLoading(object sender, EventArgs e)
            => this.SplitContainer3.Panel2Collapsed = false;

        private async void tweetThumbnail1_ThumbnailDoubleClick(object sender, ThumbnailDoubleClickEventArgs e)
            => await this.OpenThumbnailPicture(e.Thumbnail);

        private async void tweetThumbnail1_ThumbnailImageSearchClick(object sender, ThumbnailImageSearchEventArgs e)
            => await this.OpenUriInBrowserAsync(e.ImageUrl);

        private async Task OpenThumbnailPicture(ThumbnailInfo thumbnail)
        {
            var url = thumbnail.FullSizeImageUrl ?? thumbnail.MediaPageUrl;

            await this.OpenUriInBrowserAsync(url);
        }

        private async void TwitterApiStatusToolStripMenuItem_Click(object sender, EventArgs e)
            => await this.OpenUriInBrowserAsync(Twitter.ServiceAvailabilityStatusUrl);

        private void PostButton_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                this.JumpUnreadMenuItem_Click(this.JumpUnreadMenuItem, EventArgs.Empty);

                e.SuppressKeyPress = true;
            }
        }

        private void ContextMenuColumnHeader_Opening(object sender, CancelEventArgs e)
        {
            this.IconSizeNoneToolStripMenuItem.Checked = SettingManager.Common.IconSize == MyCommon.IconSizes.IconNone;
            this.IconSize16ToolStripMenuItem.Checked = SettingManager.Common.IconSize == MyCommon.IconSizes.Icon16;
            this.IconSize24ToolStripMenuItem.Checked = SettingManager.Common.IconSize == MyCommon.IconSizes.Icon24;
            this.IconSize48ToolStripMenuItem.Checked = SettingManager.Common.IconSize == MyCommon.IconSizes.Icon48;
            this.IconSize48_2ToolStripMenuItem.Checked = SettingManager.Common.IconSize == MyCommon.IconSizes.Icon48_2;

            this.LockListSortOrderToolStripMenuItem.Checked = SettingManager.Common.SortOrderLock;
        }

        private void IconSizeNoneToolStripMenuItem_Click(object sender, EventArgs e)
            => this.ChangeListViewIconSize(MyCommon.IconSizes.IconNone);

        private void IconSize16ToolStripMenuItem_Click(object sender, EventArgs e)
            => this.ChangeListViewIconSize(MyCommon.IconSizes.Icon16);

        private void IconSize24ToolStripMenuItem_Click(object sender, EventArgs e)
            => this.ChangeListViewIconSize(MyCommon.IconSizes.Icon24);

        private void IconSize48ToolStripMenuItem_Click(object sender, EventArgs e)
            => this.ChangeListViewIconSize(MyCommon.IconSizes.Icon48);

        private void IconSize48_2ToolStripMenuItem_Click(object sender, EventArgs e)
            => this.ChangeListViewIconSize(MyCommon.IconSizes.Icon48_2);

        private void ChangeListViewIconSize(MyCommon.IconSizes iconSize)
        {
            if (SettingManager.Common.IconSize == iconSize) return;

            var oldIconCol = _iconCol;

            SettingManager.Common.IconSize = iconSize;
            ApplyListViewIconSize(iconSize);

            if (_iconCol != oldIconCol)
            {
                foreach (TabPage tp in ListTab.TabPages)
                {
                    ResetColumns((DetailsListView)tp.Tag);
                }
            }

            this.CurrentListView.Refresh();
            this.MarkSettingCommonModified();
        }

        private void LockListSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var state = this.LockListSortOrderToolStripMenuItem.Checked;
            if (SettingManager.Common.SortOrderLock == state) return;

            SettingManager.Common.SortOrderLock = state;
            this.MarkSettingCommonModified();
        }

        private void tweetDetailsView_StatusChanged(object sender, TweetDetailsViewStatusChengedEventArgs e)
        {
            if (!MyCommon.IsNullOrEmpty(e.StatusText))
            {
                this.StatusLabelUrl.Text = e.StatusText;
            }
            else
            {
                this.SetStatusLabelUrl();
            }
        }
    }
}
