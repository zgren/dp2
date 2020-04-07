﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

using DigitalPlatform.Text;

namespace dp2SSL
{
    // 用于显示借书还书操作结果信息的可视化文档
    public class SubmitDocument : FlowDocument
    {
        public double BaseFontSize { get; set; }
        public string BuildStyle { get; set; }

        internal List<ActionInfo> _actions = new List<ActionInfo>();

        // 刷新显示
        // 把 actions 中的对象的状态变化更新到当前文档中
        // TODO: 一个办法是整个 Paragraph 替换。一个办法只替换里面的部分 InLine 对象
        public void Refresh(List<ActionInfo> actions)
        {
            // 更新 _actions 中的对象
            int count = 0;
            foreach (var action in actions)
            {
                var old_action = _actions.Where(o => o.ID == action.ID).FirstOrDefault();
                if (old_action != null)
                {
                    int index = _actions.IndexOf(old_action);
                    Debug.Assert(index != -1, "");
                    if (index != -1)
                    {
                        _actions.RemoveAt(index);
                        _actions.Insert(index, action);
                        count++;
                    }
                }
            }

            if (count == 0)
                return;

            {
                // 检查超额图书
                List<string> overflow_titles = new List<string>();
                _actions.ForEach(item =>
                {
                    if (item.Action == "borrow" && item.SyncErrorCode == "overflow")
                        overflow_titles.Add($"{ShortTitle(item.Entity.Title)} [{item.Entity.PII}]");
                });

                // 显示超额的信息
                if (overflow_titles.Count > 0)
                {
                    // 定位 Paragraph
                    var block = this.Blocks.Where(o =>
                    {
                        if (!(o.Tag is string id))
                            return false;
                        return id == OVERFLOW_ID;
                    }).FirstOrDefault();

                    // block 不应为 null。替代方法：
                    if (block == null)
                    {
                        // TODO: 在适当位置插入标志段落
                        throw new Exception("#overflow 标志段落没有找到");
                    }

                    var p = BuildOverflowParagraph(overflow_titles);
                    this.Blocks.InsertBefore(block, p);
                    this.Blocks.Remove(block);

                    // 语音提醒
                    App.CurrentApp.Speak("警告：借书超额");
                }
            }

            foreach (var action in actions)
            {
                // 定位 Paragraph
                var block = this.Blocks.Where(o =>
                {
                    if (!(o.Tag is ParagraphInfo info))
                        return false;
                    return info.Action.ID == action.ID;
                })
                    .FirstOrDefault();
                if (block == null)
                    continue;

                // 替换 Paragraph
                {
                    if (!(block.Tag is ParagraphInfo old_info))
                        continue;

                    var new_block = BuildParagraph(action, old_info.Index, this.BaseFontSize, this.BuildStyle);
                    this.Blocks.InsertBefore(block, new_block);
                    this.Blocks.Remove(block);
                }
            }
        }

        const string OVERFLOW_ID = "#overflow";

        // 构造超额图书列表
        Paragraph BuildOverflowParagraph(List<string> titles)
        {
            var p = new Paragraph();
            p.FontFamily = new FontFamily("微软雅黑");
            p.FontSize = this.BaseFontSize * 1.8;
            // p.FontStyle = FontStyles.Italic;
            p.TextAlignment = TextAlignment.Left;
            p.Foreground = Brushes.White;
            p.Background = Brushes.DarkGoldenrod;
            // p.LineHeight = 18;
            p.TextIndent = 0;   // -20;
            p.Margin = new Thickness(10, this.BaseFontSize * 2.0, 0, this.BaseFontSize * 2.0);
            p.Padding = new Thickness(this.BaseFontSize * 1.4);
            p.Tag = OVERFLOW_ID;

            StringBuilder text = new StringBuilder();
            text.Append($"警告：您取书已经超额了。请将下列 {titles.Count} 册图书放回书柜:");
            int i = 1;
            foreach (string title in titles)
            {
                text.Append($"\r\n{i}) {title}");
                i++;
            }
            p.Inlines.Add(new Run(text.ToString()));
            return p;
        }

        public static string ShortTitle(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int pos = text.IndexOf(". --");
            if (pos == -1)
                return text;
            return text.Substring(0, pos).Trim();
        }

        public static SubmitDocument Build(List<ActionInfo> actions,
            double baseFontSize,
            string style)
        {
            SubmitDocument doc = new SubmitDocument();
            doc.BaseFontSize = baseFontSize;
            doc.BuildStyle = style;

            // 保存起来
            doc._actions.Clear();
            doc._actions.AddRange(actions);

            bool display_transfer = StringUtil.IsInList("transfer", style);

            // 第一部分，总结信息
            List<string> names = new List<string>();
            {
                actions.ForEach((o) =>
                {
                    if (o.Operator != null)
                        names.Add(string.IsNullOrEmpty(o.Operator.PatronName) ? o.Operator.PatronBarcode : o.Operator.PatronName);
                });
                StringUtil.RemoveDupNoSort(ref names);
            }

            int return_count = actions.FindAll((o) => { return o.Action == "return"; }).Count;
            int borrow_count = actions.FindAll((o) => { return o.Action == "borrow"; }).Count;
            int transfer_count = actions.FindAll((o) => { return o.Action == "transfer"; }).Count;

            /*
            int succeed_count = actions.FindAll((o) => { return o.ResultType == "succeed" || string.IsNullOrEmpty(o.ResultType); }).Count;
            int error_count = items.FindAll((o) => { return o.ResultType == "error"; }).Count;
            int warning_count = items.FindAll((o) => { return o.ResultType == "warning"; }).Count;
            int information_count = 0;
            if (display_transfer == false)
                information_count = items.FindAll((o) => { return o.ResultType == "information" && o.Operation != "transfer"; }).Count;
            else
                information_count = items.FindAll((o) => { return o.ResultType == "information"; }).Count;

            // 检查超额图书
            List<string> overflow_titles = new List<string>();
            items.ForEach(item =>
            {
                if (item.Operation == "borrow" && item.ErrorCode == "overflow")
                    overflow_titles.Add($"{ShortTitle(item.Entity.Title)} [{item.Entity.PII}]");
            });
            */

            {
                var p = new Paragraph();
                p.FontFamily = new FontFamily("微软雅黑");
                p.FontSize = baseFontSize;
                p.TextAlignment = TextAlignment.Left;
                p.Foreground = Brushes.Gray;
                // p.TextIndent = -20;
                p.Margin = new Thickness(0, 0, 0, baseFontSize/*18*/);
                doc.Blocks.Add(p);

                if (borrow_count + return_count > 0)
                {
                    List<string> lines = new List<string>();
                    if (return_count > 0)
                        lines.Add($"还书请求 {return_count}");
                    if (borrow_count > 0)
                        lines.Add($"借书请求 {borrow_count}");

                    if (display_transfer && transfer_count > 0)
                        lines.Add($"转移请求 {transfer_count}");

                    p.Inlines.Add(new Run
                    {
                        Text = $"{StringUtil.MakePathList(names)} ",
                        //Background = Brushes.DarkRed,
                        //Foreground = Brushes.White
                        FontFamily = new FontFamily("楷体"),
                        FontSize = baseFontSize * 2.5,
                        // FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                    });

                    p.Inlines.Add(new Run
                    {
                        Text = $"{StringUtil.MakePathList(lines, ", ")}\r\n",
                        //Background = Brushes.DarkRed,
                        //Foreground = Brushes.White
                        FontSize = baseFontSize * 1.2,
                        Foreground = Brushes.White,
                    });
                }

#if NO
                if (error_count > 0)
                {
                    p.Inlines.Add(new Run
                    {
                        Text = $" 错误 {error_count} ",
                        Background = Brushes.DarkRed,
                        Foreground = Brushes.White
                    });
                }
                if (warning_count > 0)
                {
                    p.Inlines.Add(new Run
                    {
                        Text = $" 警告 {warning_count} ",
                        Background = Brushes.DarkGoldenrod,
                        Foreground = Brushes.White
                    });
                }
                if (information_count > 0)
                {
                    p.Inlines.Add(new Run
                    {
                        Text = $" 信息 {information_count} ",
                        Background = Brushes.Gray,
                        Foreground = Brushes.White
                    });
                }
                if (succeed_count > 0)
                {
                    p.Inlines.Add(new Run
                    {
                        Text = $" 成功 {succeed_count} ",
                        Background = Brushes.DarkGreen,
                        Foreground = Brushes.White
                    });
                }
#endif
            }

#if NO
            // 第二部分，列出超额的信息
            if (overflow_titles.Count > 0)
            {
                var p = BuildOverflowParagraph(overflow_titles);
                doc.Blocks.Add(p);
            }
#endif

            // 超额信息的占位符
            {
                var p = new Paragraph();
                p.FontSize = 0.1;
                p.Margin = new Thickness();
                p.Padding = new Thickness();
                p.Tag = "#overflow";
                doc.Blocks.Add(p);
            }

            // 第三部分，列出每一笔操作
            int index = 0;
            foreach (var action in actions)
            {
                var p = BuildParagraph(action, index, baseFontSize, style);
                if (p != null)
                {
                    doc.Blocks.Add(p);
                    index++;
                }
            }

#if NO
            // 构造提示语音
            List<string> speaks = new List<string>();
            if (overflow_titles.Count > 0)
            {
                speaks.Add($"警告：有 {overflow_titles.Count} 册图书超越许可册数，请放回书柜，谢谢");
            }

            if (speaks.Count == 0)
                speaks.Add("操作完成"); // TODO：可否增加姓名和借还册数？例如 王立文借书 5 册成功

            speak = StringUtil.MakePathList(speaks, "; ");
#endif

            return doc;
        }

        public class ParagraphInfo
        {
            public ActionInfo Action { get; set; }
            public int Index { get; set; }
        }

        public static Paragraph BuildParagraph(
            ActionInfo action,
            int index,
    double baseFontSize,
    string style)
        {
            bool display_transfer = StringUtil.IsInList("transfer", style);
            if (action.Action == "transfer" && display_transfer == false)
                return null;

            var p = new Paragraph();
            p.FontFamily = new FontFamily("微软雅黑");
            p.FontSize = baseFontSize;
            // p.FontStyle = FontStyles.Italic;
            p.TextAlignment = TextAlignment.Left;
            p.Foreground = Brushes.Gray;
            // p.LineHeight = 18;
            p.TextIndent = -20;
            p.Margin = new Thickness(10, 0, 0, 8);  // 10,0,0,8
            p.Tag = new ParagraphInfo { Action = action, Index = index };   // 记忆下来后面刷新事项的时候可以用到

            // 序号
            p.Inlines.Add(new Run($"{(index + 1).ToString()}) "));

            Brush back = Brushes.Transparent;
            // 状态
            {
                // 等待动画
                if (string.IsNullOrEmpty(action.State))
                {
                    var image = new FontAwesome.WPF.ImageAwesome();
                    image.Icon = FontAwesome.WPF.FontAwesomeIcon.Spinner;
                    image.Spin = true;
                    image.SpinDuration = 5;
                    image.Height = baseFontSize * 2.0;
                    image.Foreground = Brushes.DarkGray;
                    var container = new InlineUIContainer(image);
                    container.Name = "image_id";
                    p.Inlines.Add(container);
                }
                else if (action.SyncErrorCode == "overflow")
                {
                    back = Brushes.DarkRed;
                    p.Inlines.Add(new Run
                    {
                        Text = " 超额 ",
                        Background = back,
                        Foreground = Brushes.White
                    });
                }
                else if (action.State == "sync")
                {
                    back = Brushes.DarkGreen;
                    p.Inlines.Add(new Run
                    {
                        Text = " 成功 ",
                        Background = back,
                        Foreground = Brushes.White
                    });
                }
                else if (action.State == "commerror" || action.State == "normalerror")
                {
                    back = Brushes.DarkRed;
                    p.Inlines.Add(new Run
                    {
                        Text = $" 同步失败({action.State}) ",
                        Background = back,
                        Foreground = Brushes.White
                    });
                }
                else if (action.State == "dontsync")
                {
                    back = Brushes.DarkRed;
                    p.Inlines.Add(new Run
                    {
                        Text = $" 不再同步 ",
                        Background = back,
                        Foreground = Brushes.White
                    });
                }
                else
                {
                    back = Brushes.DarkRed;
                    p.Inlines.Add(new Run
                    {
                        Text = $" {action.State} ",
                        Background = back,
                        Foreground = Brushes.White
                    });
                }
            }


            // 操作名称
            p.Inlines.Add(new Run
            {
                Text = GetOperationCaption(action.Action) + " ",
                Foreground = Brushes.White
            });

            // 转移方向
            if (action.Action == "transfer" && string.IsNullOrEmpty(action.TransferDirection) == false)
            {
                p.Inlines.Add(new Run
                {
                    Text = GetOperationCaption(action.TransferDirection) + " ",
                    Foreground = Brushes.White
                });
            }

            // 书目摘要
            if (action.Entity != null && string.IsNullOrEmpty(action.Entity.Title) == false)
            {
                Run run = new Run(MessageDocument.ShortTitle(action.Entity.Title));
                /*
                run.FontSize = 14;
                run.FontStyle = FontStyles.Normal;
                run.Background = Brushes.DarkRed;
                run.Foreground = Brushes.White;
                */

                p.Inlines.Add(run);
            }

            // 错误码和错误信息
            if (string.IsNullOrEmpty(action.SyncErrorInfo) == false
                && (action.State != "sync" || action.SyncErrorCode == "overflow"))
            {
                p.Inlines.Add(new Run
                {
                    Text = "\r\n" + action.SyncErrorInfo,
                    Background = back,
                    Foreground = Brushes.White
                });
            }

            return p;
        }

        static string GetOperationCaption(string operation)
        {
            if (operation == "borrow")
                return "借";
            if (operation == "return")
                return "还";
            if (operation == "transfer")
                return "转移";
            if (operation == "changeEAS")
                return "修改EAS";

            return operation;
        }
    }
}
