// ============================================================================
//  MenuBosses.cs — вкладка «Боссы»: настройки, которые меняют самого босса в бою.
//  Список — все пресеты сразу (0.26.1): у каждого заголовок с кнопкой +/− и под ним его боссы, по строке на каждый бой (один и тот же босс в двух пресетах, или даже
//  дважды в одном, настраивается отдельно). У каждого: в какой фазе он остаётся весь бой («Все» — как в игре,
//  1, 2, 3… по числу его фаз) и кнопка «Полное здоровье». Выключено — тренировка (здоровье только этой фазы,
//  статистика не меняется); включено — челлендж (весь запас здоровья, босс весь бой в одной фазе,
//  считается в статистике отдельной строкой). Настройка лежит в самой записи пресета (Entry.Phase / Entry.Full),
//  уходит вместе с пресетом в строку «Копировать» (Share.cs) и работает только в сегментах Encore.
//  Само изменение босса — BossFx.cs, арифметика — Phase.cs.
//  Если у босса есть призывы с настройкой (мухи Broodmother, ползуны и камни Матери Мхов — SummonData.cs), под строкой идёт вторая:
//  по кнопке на каждый лимит («Как в игре», число, «выкл»); он лежит в записи пресета (Entry.Sum1 / Entry.Sum2), бой с лимитом — тренировка.
//  Ещё одна строка — «Здоровье» (у всех боссов из PhaseLock.Defs и PhaseLock.SingleDefs): поле с числом 1…9999 и кнопка «Как в игре»;
//  число лежит в записи пресета (Entry.Hp). С выбранной фазой это здоровье этой фазы, без неё — всего боя; бой с ним — тренировка.
//  И ещё одна — «Здоровье призывов» (у боссов из SummonData.HpKinds: вороны, враги Зверомух, подкрепление Старших братьев, Щепка): поле на каждый
//  вид призыва, в скобках — сколько в игре; пустое поле — как в игре. Числа лежат в записи пресета (Entry.Sh1…Sh3), бой с ними — тренировка.
//  И «Шансы призывов» (0.26.0; у Отца раков, Матери Мхов, Зверомух — SummonData.Chance): проценты видов призыва (в сумме 100, остальные подстраиваются сами)
//  и шанс самого призыва; лежат в записи пресета (Entry.Sc1…Sc3 / Entry.Sp), бой с ними — тренировка.
//  У каждого босса дополнительные строки (призывы, здоровье, шансы) прячутся кнопкой слева от номера; в шапке — «Раскрыть все» / «Свернуть все».
//  Список прокручивается колёсиком мыши. Что менять: новый босс — запись в PhaseLock.Defs, новый призыв — в SummonData.Build.
// ============================================================================
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        bool showBosses;
        Vector2 scrollBosses;
        /// Что набрано в поле своего здоровья у каждой записи пресета (пока не открыли окно заново); пусто — по записи.
        readonly System.Collections.Generic.Dictionary<Entry, string> hpEdit = new System.Collections.Generic.Dictionary<Entry, string>();
        /// То же для здоровья призывов: по одному тексту на вид призыва (null — по записи).
        readonly System.Collections.Generic.Dictionary<Entry, string[]> shpEdit = new System.Collections.Generic.Dictionary<Entry, string[]>();

        /// Боссы (строки пресета), у которых дополнительные строки свёрнуты. Только в окне, в пресет не пишется; по умолчанию всё раскрыто.
        readonly System.Collections.Generic.HashSet<Entry> folded = new System.Collections.Generic.HashSet<Entry>();
        /// Вид вкладки: false — все боссы одним списком (настройка одна на босса, действует во всех пресетах), true — по пресетам, у каждого свои бои.
        bool byPreset;
        /// Для вида «Все боссы»: по одной «общей» записи на босса (в пресеты не входит). Её показывают и правят; что изменилось — тут же копируется во все его бои.
        readonly System.Collections.Generic.Dictionary<string, Entry> proxies = new System.Collections.Generic.Dictionary<string, Entry>();
        /// Пресеты, у которых список боссов свёрнут до заголовка. Тоже только в окне.
        readonly System.Collections.Generic.HashSet<Preset> foldedPresets = new System.Collections.Generic.HashSet<Preset>();

        const float PresetHeadH = 44f, PresetMsgH = 38f;
        const float ChLabelW = 104f, ChBtnW = 32f, ChBtnGap = 3f, ChValW = 58f, ChGroupGap = 14f, ChAsIsW = 140f, ChAsIsX = 200f;
        const float BossSetRowH = 54f, BossBtnH = 34f, BossBtnGap = 8f, BossAllW = 100f, BossNumW = 64f, BossFullW = 250f;
        const float BossNameW = 400f, BossCountW = 110f, BossArrowW = 44f;
        const float SumRowH = 42f, SumLabelW = 130f, SumVanillaW = 130f, SumNumW = 64f, SumOffW = 90f, SumGroupGap = 28f;
        const float HpRowH = 42f, HpFieldW = 110f;
        const int AtkPerLine = 5;   // кнопок в строке атак (первая — «Все»)
        const float SumHpLabelW = 172f, SumHpFieldW = 80f, SumHpHintW = 50f, SumHpGroupGap = 14f;

        /// Размеры строки босса: считаются один раз за кадр и передаются в DrawBossBlock.
        sealed class BossLayout
        {
            internal float rowH, sumH, hpH, numberW, nameX, countX, buttonsX, btnH, gap, fullW, fullX, width, headH, msgH;
            internal string info;   // пояснение справа от кнопок фаз (вид «Все боссы»): сколько боёв и одинаковы ли настройки
        }

        void DrawBosses()
        {
            float panelW = WinW - 2f * Pad;
            Panel(Pad, panelW, L.T("Настройки боссов", "Boss settings"));
            System.Collections.Generic.List<Preset> presets = PresetStore.Data.Presets;

            // справа в шапке: вид («Все боссы» / «По пресетам») и «Раскрыть все» / «Свернуть все»
            float right = Pad + panelW - Inner;
            float foldW = 190f, modeW = 250f;
            if (GUI.Button(R(right - foldW, Top + 9f, foldW, 28f), Theme.Caps(L.T("Свернуть все", "Collapse all")), Theme.GhostSmall))
            {
                if (byPreset) foreach (Preset q in presets) foldedPresets.Add(q);
                else foreach (Entry px in proxies.Values) folded.Add(px);
            }
            if (GUI.Button(R(right - 2f * foldW - 8f, Top + 9f, foldW, 28f), Theme.Caps(L.T("Раскрыть все", "Expand all")), Theme.GhostSmall))
            {
                foldedPresets.Clear();
                folded.Clear();
            }
            if (GUI.Button(R(right - 2f * foldW - 8f - 14f - modeW, Top + 9f, modeW, 28f),
                    Theme.Caps(byPreset ? L.T("Вид: по пресетам", "View: by presets") : L.T("Вид: все боссы", "View: all bosses")), Theme.TabActive))
            {
                byPreset = !byPreset;
                scrollBosses = Vector2.zero;
            }

            float viewTop = ListTop;
            float viewBottom = Top + ColH - Inner;
            Rect view = R(Pad + 10f, viewTop, panelW - 20f, viewBottom - viewTop);
            if (presets.Count == 0)
            {
                GUI.Label(view, L.T("Пресетов нет.", "There are no presets."), Theme.SmallCenter);
                return;
            }
            if (!byPreset)
            {
                DrawAllBosses(view, presets);
                return;
            }

            var lay = new BossLayout();
            lay.rowH = Theme.Px(BossSetRowH);
            lay.sumH = Theme.Px(SumRowH);
            lay.hpH = Theme.Px(HpRowH);
            lay.headH = Theme.Px(PresetHeadH);
            lay.msgH = Theme.Px(PresetMsgH);
            float content = 0f;
            foreach (Preset q in presets) content += lay.headH + (foldedPresets.Contains(q) ? 0f : PresetBodyHeight(q, lay));
            float width = BeginList(view, ref scrollBosses, content);

            lay.width = width;
            lay.numberW = Theme.Px(34f);
            lay.nameX = Theme.Px(90f);
            lay.countX = lay.nameX + Theme.Px(BossNameW);
            lay.buttonsX = lay.countX + Theme.Px(BossCountW);
            lay.btnH = Theme.Px(BossBtnH);
            lay.gap = Theme.Px(BossBtnGap);
            lay.fullW = Theme.Px(BossFullW);
            lay.fullX = width - lay.fullW - Theme.Px(14f);

            float y = 0f;
            foreach (Preset q in presets) y = DrawPresetGroup(q, y, lay);
            GUI.EndScrollView();
        }

        /// Что копируется из «общей» записи в бои босса: все настройки боя (сложность и сам босс остаются как были).
        static void CopySettings(Entry from, Entry to)
        {
            to.Phase = from.Phase; to.Full = from.Full; to.Sum1 = from.Sum1; to.Sum2 = from.Sum2; to.Hp = from.Hp;
            to.Sh1 = from.Sh1; to.Sh2 = from.Sh2; to.Sh3 = from.Sh3; to.Imm = from.Imm;
            to.Sc1 = from.Sc1; to.Sc2 = from.Sc2; to.Sc3 = from.Sc3; to.Sp = from.Sp; to.At = from.At;
        }

        static string Sig(Entry e)
        {
            return e.Phase + "|" + e.Full + "|" + e.Sum1 + "|" + e.Sum2 + "|" + e.Hp + "|" + e.Sh1 + "|" + e.Sh2 + "|" + e.Sh3 + "|" + e.Imm + "|" + e.Sc1 + "|" + e.Sc2 + "|" + e.Sc3 + "|" + e.Sp + "|" + e.At;
        }

        /// Боссы вида «Все боссы»: сначала по порядку Пантеона 5, потом остальные, у которых есть настройка, потом любые другие из пресетов.
        static System.Collections.Generic.List<string> BossRoster(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Entry>> byBoss)
        {
            System.Collections.Generic.List<string> list = Defaults.P5Bosses();
            foreach (BossDef d in PhaseLock.Defs) if (!list.Contains(d.Boss)) list.Add(d.Boss);
            foreach (BossDef d in PhaseLock.SingleDefs) if (!list.Contains(d.Boss)) list.Add(d.Boss);
            foreach (string b in byBoss.Keys) if (!list.Contains(b)) list.Add(b);
            return list;
        }

        /// Вид «Все боссы»: каждый босс игры одной строкой (с дополнительными под ней). Настройка одна на босса: что изменили — копируется во все его бои
        /// во всех пресетах, где порядок не спрятан (у спрятанных пресетов список боссов не выдаём). Босс, которого нет ни в одном пресете, показан серым без кнопок.
        void DrawAllBosses(Rect view, System.Collections.Generic.List<Preset> presets)
        {
            var byBoss = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Entry>>();
            foreach (Preset q in presets)
            {
                if (q.Hidden) continue;
                foreach (Entry e in q.Entries)
                {
                    if (e.IsRest || e.Boss == null) continue;
                    System.Collections.Generic.List<Entry> list;
                    if (!byBoss.TryGetValue(e.Boss, out list)) { list = new System.Collections.Generic.List<Entry>(); byBoss[e.Boss] = list; }
                    list.Add(e);
                }
            }
            System.Collections.Generic.List<string> roster = BossRoster(byBoss);

            // «общая» запись каждого босса повторяет его первый бой (если что-то поменяли в другом месте — подтягиваем)
            foreach (string boss in roster)
            {
                System.Collections.Generic.List<Entry> list;
                if (!byBoss.TryGetValue(boss, out list)) continue;
                Entry px;
                if (!proxies.TryGetValue(boss, out px)) { px = new Entry { Boss = boss }; proxies[boss] = px; }
                px.Difficulty = list[0].Difficulty;
                if (Sig(px) != Sig(list[0])) CopySettings(list[0], px);
            }

            var lay = new BossLayout();
            lay.rowH = Theme.Px(BossSetRowH);
            lay.sumH = Theme.Px(SumRowH);
            lay.hpH = Theme.Px(HpRowH);
            float content = 0f;
            foreach (string boss in roster)
            {
                Entry px;
                bool has = byBoss.ContainsKey(boss) && proxies.TryGetValue(boss, out px);
                content += lay.rowH;
                if (has && !folded.Contains(proxies[boss])) content += ExtraHeight(proxies[boss], lay.sumH, lay.hpH);
            }
            float width = BeginList(view, ref scrollBosses, content);

            lay.width = width;
            lay.numberW = Theme.Px(34f);
            lay.nameX = Theme.Px(90f);
            lay.countX = lay.nameX + Theme.Px(BossNameW);
            lay.buttonsX = lay.countX + Theme.Px(BossCountW);
            lay.btnH = Theme.Px(BossBtnH);
            lay.gap = Theme.Px(BossBtnGap);
            lay.fullW = Theme.Px(BossFullW);
            lay.fullX = width - lay.fullW - Theme.Px(14f);

            float y = 0f;
            int row = 0;
            foreach (string boss in roster)
            {
                row++;
                System.Collections.Generic.List<Entry> list;
                if (!byBoss.TryGetValue(boss, out list))
                {
                    if (row % 2 == 0) GUI.DrawTexture(new Rect(0f, y, width, lay.rowH), Theme.Zebra);
                    GUI.Label(new Rect(Theme.Px(44f), y, lay.numberW, lay.rowH), Theme.Digits(row), Theme.Number);
                    GUI.Label(new Rect(lay.nameX, y, Theme.Px(BossNameW) - Theme.Px(10f), lay.rowH), Runner.Display(boss), Theme.EntryTextDim);
                    GUI.Label(new Rect(lay.countX, y, width - lay.countX, lay.rowH), L.T("нет ни в одном пресете", "in no preset"), Theme.EntryTextDim);
                    y += lay.rowH;
                    continue;
                }
                Entry px = proxies[boss];
                bool differ = false;
                string first = Sig(list[0]);
                foreach (Entry e in list) if (Sig(e) != first) { differ = true; break; }
                lay.info = L.T("боёв: ", "fights: ") + list.Count + (differ ? L.T(" · настройки разные, правка выровняет", " · settings differ, an edit evens them out") : "");
                string before = Sig(px);
                y += DrawBossBlock(px, row, y, lay);
                if (Sig(px) != before)
                {
                    // изменили в «общей» записи — во все бои этого босса
                    foreach (Entry e in list) CopySettings(px, e);
                    PresetStore.PhaseRevision++;
                    PresetStore.Save();
                }
            }
            lay.info = null;
            GUI.EndScrollView();
        }

        /// Высота боссов пресета под его заголовком (развёрнутого): строки боссов или одна строка-пояснение.
        float PresetBodyHeight(Preset q, BossLayout lay)
        {
            if (q.Hidden || q.BossCount == 0) return lay.msgH;
            float h = 0f;
            foreach (Entry b in q.Entries)
                if (!b.IsRest) h += lay.rowH + (folded.Contains(b) ? 0f : ExtraHeight(b, lay.sumH, lay.hpH));
            return h;
        }

        /// Пресет в списке: заголовок (кнопка +/−, имя, сколько боссов и сколько настроено) и под ним его боссы. Возвращает y после группы.
        float DrawPresetGroup(Preset q, float y, BossLayout lay)
        {
            bool isFolded = foldedPresets.Contains(q);
            float head = lay.headH;
            GUI.DrawTexture(new Rect(0f, y, lay.width, head), Theme.Zebra);
            float fbH = Theme.Px(BossBtnH);
            if (GUI.Button(new Rect(Theme.Px(4f), y + Mathf.Round((head - fbH) / 2f), Theme.Px(34f), fbH), isFolded ? "+" : "-", isFolded ? Theme.TabActive : Theme.Tab))
            {
                if (isFolded) foldedPresets.Remove(q); else foldedPresets.Add(q);
            }
            GUI.Label(new Rect(Theme.Px(48f), y, lay.buttonsX - Theme.Px(60f), head), q.Name, Theme.PresetText);
            string info;
            if (q.Hidden) info = L.T("порядок спрятан", "order hidden");
            else
            {
                int set = 0;
                foreach (Entry b in q.Entries) if (!b.IsRest && b.HasSettings) set++;
                info = L.T("боссов: ", "bosses: ") + q.BossCount + (set > 0 ? L.T("  ·  настроено: ", "  ·  set: ") + set : "");
            }
            if (q == PresetStore.Selected) info += L.T("  ·  выбран", "  ·  selected");
            GUI.Label(new Rect(lay.buttonsX, y, lay.width - lay.buttonsX, head), info, Theme.EntryTextDim);
            y += head;
            if (isFolded) return y;

            if (q.Hidden || q.BossCount == 0)
            {
                // кнопки и число фаз выдали бы, что за босс стоит в строке
                string msg = q.Hidden ? L.T("Порядок пресета спрятан. Нажми «Показать» на вкладке «Пресеты», чтобы настроить боссов.",
                                            "The preset order is hidden. Press \"Reveal\" on the Presets tab to set the bosses up.")
                                      : L.T("В пресете нет боссов.", "The preset has no bosses.");
                GUI.Label(new Rect(lay.nameX, y, lay.width - lay.nameX, lay.msgH), msg, Theme.EntryTextDim);
                return y + lay.msgH;
            }

            int row = 0;
            foreach (Entry e in q.Entries)
            {
                if (e.IsRest) continue;
                y += DrawBossBlock(e, ++row, y, lay);
            }
            return y;
        }

        /// Один бой пресета: первая строка (фазы, полное здоровье) и, если не свёрнуто, дополнительные. Возвращает высоту блока.
        float DrawBossBlock(Entry e, int row, float y, BossLayout lay)
        {
            float rowH = lay.rowH, sumH = lay.sumH, hpH = lay.hpH, width = lay.width;
            float nameX = lay.nameX, countX = lay.countX, buttonsX = lay.buttonsX, btnH = lay.btnH, gap = lay.gap, fullW = lay.fullW, fullX = lay.fullX;
            bool hasSummons = SummonData.Count(e.Boss) > 0 && PhaseLock.Count(e.Boss) > 0;
            bool hasHp = PhaseLock.HpCapable(e.Boss);
            bool hasSummonHp = SummonData.HpCount(e.Boss) > 0 && PhaseLock.Count(e.Boss) > 0;
            bool hasChance = SummonData.HasChance(e.Boss) && PhaseLock.Count(e.Boss) > 0;
            bool isFolded = folded.Contains(e);
            float blockH = rowH + (isFolded ? 0f : ExtraHeight(e, sumH, hpH));
            if (row % 2 == 0) GUI.DrawTexture(new Rect(0f, y, width, blockH), Theme.Zebra);
            GUI.Label(new Rect(Theme.Px(44f), y, lay.numberW, rowH), Theme.Digits(row), Theme.Number);
            GUI.Label(new Rect(nameX, y, Theme.Px(BossNameW) - Theme.Px(10f), rowH), Runner.Display(e.Boss), Theme.EntryText);
            // кнопка «свернуть / раскрыть» — у боссов, у которых есть дополнительные строки
            if (ExtraHeight(e, sumH, hpH) > 0f)
            {
                bool custom = HasCustom(e);
                string foldText = isFolded ? "+" : "-";
                float fbH = Theme.Px(BossBtnH);
                if (GUI.Button(new Rect(Theme.Px(4f), y + Mathf.Round((rowH - fbH) / 2f), Theme.Px(34f), fbH), foldText, isFolded && custom ? Theme.TabActive : Theme.Tab))
                {
                    if (isFolded) folded.Remove(e); else folded.Add(e);
                }
            }

            int count = PhaseLock.Count(e.Boss);
            if (count == 0)
            {
                // босс без фаз: фазы и призывов нет, но здоровье можно задать (вторая строка)
                GUI.Label(new Rect(countX, y, width - countX, rowH), hasHp ? L.T("без фаз", "no phases") : L.T("настройки нет", "no setting"), Theme.EntryTextDim);
                if (hasHp && !isFolded) DrawHp(e, y + rowH, hpH, nameX, buttonsX, btnH, gap, fullX, fullW);
                return blockH;
            }
            GUI.Label(new Rect(countX, y, Theme.Px(BossCountW), rowH), PhaseLock.CountText(count), Theme.EntryTextDim);

            int current; bool full;
            PresetStore.GetPhase(e, out current, out full);
            float by = y + Mathf.Round((rowH - btnH) / 2f);
            float x = buttonsX;
            for (int m = 0; m <= count; m++)
            {
                float w = Theme.Px(m == 0 ? BossAllW : BossNumW);
                string text = m == 0 ? L.T("Все", "All") : m.ToString();
                if (GUI.Button(new Rect(x, by, w, btnH), Theme.Caps(text), m == current ? Theme.TabActive : Theme.Tab) && m != current)
                {
                    PresetStore.SetPhase(e, m, full);
                    status = Runner.Display(e.Boss) + ": " + PhaseLock.Describe(m, full) + ".";
                }
                x += w + gap;
            }
            if (lay.info != null && fullX - x > Theme.Px(150f))
                GUI.Label(new Rect(x + Theme.Px(10f), y, fullX - x - Theme.Px(20f), rowH), lay.info, Theme.SmallRight);

            string fullText = full ? L.T("Полное здоровье: вкл", "Full health: on") : L.T("Полное здоровье: выкл", "Full health: off");
            if (GUI.Button(new Rect(fullX, by, fullW, btnH), Theme.Caps(fullText), full ? Theme.Danger : Theme.Ghost))
            {
                PresetStore.SetPhase(e, current, !full);
                status = Runner.Display(e.Boss) + ": " + PhaseLock.Describe(current, !full) + ".";
            }

            if (isFolded) return blockH;
            float below = y + rowH;
            if (hasSummons) { DrawSummons(e, below, sumH, nameX, buttonsX, btnH, gap); below += sumH; }
            if (hasChance)
            {
                if (SummonData.TypeCount(e.Boss) > 0) { DrawTypeChances(e, below, sumH, nameX, buttonsX, btnH, gap); below += sumH; }
                if (SummonData.HasCall(e.Boss)) { DrawCallChance(e, below, sumH, nameX, buttonsX, btnH, gap); below += sumH; }
            }
            if (hasHp) { DrawHp(e, below, hpH, nameX, buttonsX, btnH, gap, fullX, fullW); below += hpH; }
            if (hasSummonHp) { DrawSummonHp(e, below, hpH, nameX, buttonsX, btnH, gap); below += hpH; }
            if (AttackData.Has(e.Boss)) DrawAttacks(e, below, sumH, nameX, btnH, gap, width);
            return blockH;
        }

        /// Высота дополнительных строк босса (всё под первой строкой): призывы, шансы, здоровье, здоровье призывов. 0 — их нет.
        float ExtraHeight(Entry e, float sumH, float hpH)
        {
            bool phased = PhaseLock.Count(e.Boss) > 0;
            float h = 0f;
            if (SummonData.Count(e.Boss) > 0 && phased) h += sumH;
            if (SummonData.HasChance(e.Boss) && phased)
            {
                if (SummonData.TypeCount(e.Boss) > 0) h += sumH;
                if (SummonData.HasCall(e.Boss)) h += sumH;
            }
            if (PhaseLock.HpCapable(e.Boss)) h += hpH;
            if (SummonData.HpCount(e.Boss) > 0 && phased) h += hpH;
            if (AttackData.Has(e.Boss) && phased) h += sumH * AttackLines(e.Boss);
            return h;
        }

        /// Есть ли в дополнительных строках босса что-то заданное (чтобы свёрнутую кнопку было видно).
        static bool HasCustom(Entry e)
        {
            return SummonData.Any(SummonData.Limits(e)) || SummonData.AnyChance(SummonData.ChanceValues(e)) || PresetStore.GetHp(e) > 0
                || SummonData.AnyHp(SummonData.HpValues(e)) || PresetStore.GetImmortal(e) || AttackData.Mask(e) != 0;
        }

        /// Группа «имя  −5 −1  значение  +1 +5»: рисует её от x (x сдвигается за неё) и возвращает новое значение (то же, если не нажали).
        int Stepper(ref float x, float y, float h, float by, float btnH, string label, int value, int min, int max)
        {
            float bw = Theme.Px(ChBtnW), bg = Theme.Px(ChBtnGap);
            if (label.Length > 0)
            {
                GUI.Label(new Rect(x, y, Theme.Px(ChLabelW), h), label, Theme.EntryTextDim);
                x += Theme.Px(ChLabelW);
            }
            int result = value;
            int[] steps = { -5, -1, 1, 5 };
            bool prev = GUI.enabled;
            for (int s = 0; s < steps.Length; s++)
            {
                if (s == 2)
                {
                    GUI.enabled = prev;
                    GUI.Label(new Rect(x, y, Theme.Px(ChValW), h), value + " %", Theme.SmallCenter);
                    x += Theme.Px(ChValW) + bg;
                }
                bool can = steps[s] < 0 ? value > min : value < max;
                GUI.enabled = prev && can;
                string text = (steps[s] > 0 ? "+" : "-") + Mathf.Abs(steps[s]);
                if (GUI.Button(new Rect(x, by, bw, btnH), text, Theme.GhostSmall))
                    result = Mathf.Clamp(value + steps[s], min, max);
                x += bw + bg;
            }
            GUI.enabled = prev;
            return result;
        }

        static bool SameAs(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// Строка «Шанс видов»: по группе на каждый вид призыва (проценты в сумме 100); «Как в игре» — вернуть. Меняешь один — остальные подстраиваются.
        void DrawTypeChances(Entry e, float y, float h, float nameX, float buttonsX, float btnH, float gap)
        {
            ChanceSpec c = SummonData.Chance(e.Boss);
            int[] cur = SummonData.TypePercents(e);
            bool custom = SummonData.TypesOrNull(e) != null;
            GUI.Label(new Rect(nameX, y, Theme.Px(ChAsIsX), h), L.T("Шанс видов", "Kind chances"), Theme.EntryTextDim);
            float by = y + Mathf.Round((h - btnH) / 2f);
            if (GUI.Button(new Rect(nameX + Theme.Px(ChAsIsX), by, Theme.Px(ChAsIsW), btnH), Theme.Caps(L.T("Как в игре", "As in game")), custom ? Theme.Tab : Theme.TabActive) && custom)
            {
                PresetStore.SetTypeChances(e, null);
                status = Runner.Display(e.Boss) + ": " + L.T("шансы видов как в игре", "kind chances as in the game") + ".";
            }
            float x = buttonsX;
            for (int k = 0; k < c.Types; k++)
            {
                int value = Stepper(ref x, y, h, by, btnH, c.TypeName(k), cur[k], 0, 100);
                x += Theme.Px(ChGroupGap);
                if (value == cur[k]) continue;
                int[] next = SummonData.Rebalance(cur, k, value);
                PresetStore.SetTypeChances(e, SameAs(next, c.Vanilla) ? null : next);
                status = Runner.Display(e.Boss) + ": " + SummonData.DescribeChance(e.Boss, SummonData.ChanceValues(e)) + (SameAs(next, c.Vanilla) ? L.T("как в игре", "as in the game") : "") + ".";
                cur = next;
            }
        }

        /// Строка шанса самого призыва (зов воронов, рёв Зверомухи): одна группа и подсказка, сколько в игре.
        void DrawCallChance(Entry e, float y, float h, float nameX, float buttonsX, float btnH, float gap)
        {
            ChanceSpec c = SummonData.Chance(e.Boss);
            int stored = SummonData.GetCall(e);
            bool custom = stored != SummonData.Vanilla;
            int cur = custom ? stored : c.CallVanilla;
            GUI.Label(new Rect(nameX, y, Theme.Px(ChAsIsX), h), L.T("Шанс: ", "Chance: ") + c.CallName.ToLower(), Theme.EntryTextDim);
            float by = y + Mathf.Round((h - btnH) / 2f);
            if (GUI.Button(new Rect(nameX + Theme.Px(ChAsIsX), by, Theme.Px(ChAsIsW), btnH), Theme.Caps(L.T("Как в игре", "As in game")), custom ? Theme.Tab : Theme.TabActive) && custom)
            {
                PresetStore.SetCallChance(e, SummonData.Vanilla);
                status = Runner.Display(e.Boss) + ": " + L.T("шанс как в игре", "chance as in the game") + ".";
            }
            float x = buttonsX;
            int value = Stepper(ref x, y, h, by, btnH, "", cur, 0, c.CallMax);
            if (value != cur)
            {
                PresetStore.SetCallChance(e, value == c.CallVanilla ? SummonData.Vanilla : value);
                status = Runner.Display(e.Boss) + ": " + c.CallName.ToLower() + " " + value + "%.";
            }
            x += Theme.Px(ChGroupGap);
            string hint = L.T("в игре: ", "game: ") + c.CallVanilla + " %";
            if (c.CallMax < 100) hint += L.T("  ·  не больше " + c.CallMax + " %: иначе босс без конца выбирает отказавшийся рёв", "  ·  at most " + c.CallMax + " %: a refused roar would be chosen again and again");
            GUI.Label(new Rect(x, y, Theme.Px(640f), h), hint, Theme.EntryTextDim);
        }

        /// Строка «Здоровье»: поле с числом (1…9999) и кнопка «Как в игре». Число действует сразу, как его набрали; пусто или 0 — как в игре.
        void DrawHp(Entry e, float y, float h, float nameX, float buttonsX, float btnH, float gap, float immX, float immW)
        {
            GUI.Label(new Rect(nameX, y, buttonsX - nameX, h), L.T("Здоровье", "Health"), Theme.EntryTextDim);
            float by = y + Mathf.Round((h - btnH) / 2f);
            float x = buttonsX;
            int current = PresetStore.GetHp(e);

            string text;
            if (!hpEdit.TryGetValue(e, out text)) text = current > 0 ? Theme.Digits(current) : "";
            float fieldW = Theme.Px(HpFieldW);
            string typed = GUI.TextField(new Rect(x, by, fieldW, btnH), text, 4, Theme.Field);
            if (typed != text)
            {
                int value;
                string digits = DigitsOnly(typed);
                if (!int.TryParse(digits, out value)) value = 0;
                value = Mathf.Clamp(value, 0, PhaseLock.HpMax);
                hpEdit[e] = value > 0 ? Theme.Digits(value) : digits.Length > 0 ? "0" : "";
                PresetStore.SetHp(e, value);
                current = PresetStore.GetHp(e);
                status = Runner.Display(e.Boss) + ": " + (current > 0 ? L.T("здоровье ", "health ") + current : L.T("здоровье как в игре", "health as in the game")) + ".";
            }
            x += fieldW + gap;

            if (GUI.Button(new Rect(x, by, Theme.Px(SumVanillaW), btnH), Theme.Caps(L.T("Как в игре", "As in game")), current == 0 ? Theme.TabActive : Theme.Tab) && current != 0)
            {
                PresetStore.SetHp(e, 0);
                hpEdit.Remove(e);
                status = Runner.Display(e.Boss) + ": " + L.T("здоровье как в игре", "health as in the game") + ".";
            }
            x += Theme.Px(SumVanillaW) + gap;

            int vanilla = BossFx.VanillaHp(e.Boss, e.Difficulty);
            string hint = vanilla > 0 ? L.T("в игре: ", "game: ") + vanilla : "";
            if (hint.Length > 0) GUI.Label(new Rect(x, y, Theme.Px(260f), h), hint, Theme.EntryTextDim);

            // бессмертие (0.22.0): справа, под кнопкой «Полное здоровье»; у боссов с отдельным запасом здоровья на фазу кнопки нет
            if (PhaseLock.ImmortalCapable(e.Boss))
            {
                bool imm = PresetStore.GetImmortal(e);
                string immText = imm ? L.T("Бессмертие: вкл", "Immortal: on") : L.T("Бессмертие: выкл", "Immortal: off");
                if (GUI.Button(new Rect(immX, by, immW, btnH), Theme.Caps(immText), imm ? Theme.Danger : Theme.Ghost))
                {
                    PresetStore.SetImmortal(e, !imm);
                    status = Runner.Display(e.Boss) + ": " + (!imm ? L.T("бессмертие включено (бой — тренировка)", "immortal on (the fight is practice)")
                                                                   : L.T("бессмертие выключено", "immortal off")) + ".";
                }
            }
        }

        /// Строка «Здоровье призывов»: по полю с числом (1…9999) на каждый вид призыва, в скобках — сколько в игре. Число действует сразу, как его набрали; пусто — как в игре.
        void DrawSummonHp(Entry e, float y, float h, float nameX, float buttonsX, float btnH, float gap)
        {
            SummonHpKind[] kinds = SummonData.HpKinds(e.Boss);
            GUI.Label(new Rect(nameX, y, buttonsX - nameX, h), L.T("Здоровье призывов", "Summon health"), Theme.EntryTextDim);
            float by = y + Mathf.Round((h - btnH) / 2f);
            float x = buttonsX;
            string[] texts;
            if (!shpEdit.TryGetValue(e, out texts))
            {
                texts = new string[SummonData.HpSlots];
                shpEdit[e] = texts;
            }
            for (int k = 0; k < kinds.Length; k++)
            {
                int current = SummonData.GetHp(e, k);
                GUI.Label(new Rect(x, y, Theme.Px(SumHpLabelW), h), kinds[k].Name, Theme.EntryTextDim);
                x += Theme.Px(SumHpLabelW);

                string text = texts[k] != null ? texts[k] : (current > 0 ? Theme.Digits(current) : "");
                string typed = GUI.TextField(new Rect(x, by, Theme.Px(SumHpFieldW), btnH), text, 4, Theme.Field);
                if (typed != text)
                {
                    int value;
                    string digits = DigitsOnly(typed);
                    if (!int.TryParse(digits, out value)) value = 0;
                    value = Mathf.Clamp(value, 0, PhaseLock.HpMax);
                    texts[k] = value > 0 ? Theme.Digits(value) : digits.Length > 0 ? "0" : "";
                    PresetStore.SetSummonHp(e, k, value);
                    current = SummonData.GetHp(e, k);
                    status = Runner.Display(e.Boss) + ": " + kinds[k].Name + " — "
                        + (current > 0 ? L.T("здоровье ", "health ") + current : L.T("как в игре", "as in the game")) + ".";
                }
                x += Theme.Px(SumHpFieldW) + gap;

                int vanilla = SummonHp.VanillaHp(kinds[k].Key, e.Difficulty);
                if (vanilla > 0) GUI.Label(new Rect(x, y, Theme.Px(SumHpHintW), h), "(" + vanilla + ")", Theme.EntryTextDim);
                x += Theme.Px(SumHpHintW) + Theme.Px(SumHpGroupGap);
            }
        }

        /// Сколько строк занимают кнопки атак босса: «Все» и по кнопке на каждую атаку, по AtkPerLine в строке.
        static int AttackLines(string boss)
        {
            return (AttackData.Count(boss) + 1 + AtkPerLine - 1) / AtkPerLine;
        }

        /// Строки «Атаки»: «Все» (включить все) и по кнопке на атаку. Левый клик — выключить или включить атаку, правый — оставить только её.
        /// Включённая атака горит, выключенная тусклая; последнюю включённую выключить нельзя. Бой с выключенными атаками — тренировка.
        void DrawAttacks(Entry e, float y, float lineH, float nameX, float btnH, float gap, float width)
        {
            AttackKind[] kinds = AttackData.Kinds(e.Boss);
            int mask = AttackData.Mask(e);
            GUI.Label(new Rect(nameX, y, Theme.Px(200f), lineH), L.T("Атаки", "Attacks"), Theme.EntryTextDim);
            if (kinds.Length + 1 > AtkPerLine)
                GUI.Label(new Rect(nameX, y + lineH, Theme.Px(200f), lineH), L.T("ЛКМ вкл/выкл · ПКМ только эта", "LMB on/off · RMB only this"), Theme.Small);
            float x0 = nameX + Theme.Px(210f);
            float bw = Mathf.Max(Theme.Px(60f), (width - x0 - Theme.Px(24f) - gap * (AtkPerLine - 1)) / AtkPerLine);
            for (int item = 0; item <= kinds.Length; item++)
            {
                int line = item / AtkPerLine, col = item % AtkPerLine;
                float bx = x0 + col * (bw + gap);
                float by = y + line * lineH + Mathf.Round((lineH - btnH) / 2f);
                Rect box = new Rect(bx, by, bw, btnH);
                if (item == 0)
                {
                    if (GUI.Button(box, Theme.Caps(L.T("Все", "All")), mask == 0 ? Theme.TabActive : Theme.Tab) && mask != 0)
                    {
                        PresetStore.ResetAttacks(e);
                        status = Runner.Display(e.Boss) + ": " + L.T("все атаки включены.", "all attacks on.");
                    }
                    continue;
                }
                int k = item - 1;
                bool off = (mask & (1 << k)) != 0;
                Event ev = Event.current;
                bool right = ev.type == EventType.MouseDown && ev.button == 1 && box.Contains(ev.mousePosition);
                if (right)
                {
                    ev.Use();
                    PresetStore.SetOnlyAttack(e, k);
                    status = Runner.Display(e.Boss) + ": " + L.T("только «", "only \"") + kinds[k].Name + L.T("».", "\".");
                    continue;
                }
                if (GUI.Button(box, Theme.Caps(kinds[k].Name), off ? Theme.Tab : Theme.TabActive))
                {
                    if (PresetStore.SetAttack(e, k, !off))
                        status = Runner.Display(e.Boss) + ": " + kinds[k].Name + " — " + (off ? L.T("включена", "on") : L.T("выключена", "off"))
                            + L.T(". Включено атак: ", ". Attacks on: ") + AttackData.Enabled(e.Boss, AttackData.Mask(e)) + L.T(" из ", " of ") + kinds.Length + ".";
                    else
                        status = Runner.Display(e.Boss) + ": " + L.T("хоть одна атака должна остаться включённой.", "at least one attack must stay on.");
                }
            }
        }

        /// Вторая строка босса: по группе кнопок на каждый вид призыва — «Как в игре», числа от большего к меньшему, «выкл».
        void DrawSummons(Entry e, float y, float h, float nameX, float buttonsX, float btnH, float gap)
        {
            SummonKind[] kinds = SummonData.Kinds(e.Boss);
            GUI.Label(new Rect(nameX, y, buttonsX - nameX, h), L.T("Призывы", "Summons"), Theme.EntryTextDim);
            float by = y + Mathf.Round((h - btnH) / 2f);
            float x = buttonsX;
            for (int k = 0; k < kinds.Length; k++)
            {
                int current = SummonData.Get(e, k);
                GUI.Label(new Rect(x, y, Theme.Px(SumLabelW), h), kinds[k].Name, Theme.EntryTextDim);
                x += Theme.Px(SumLabelW);
                // варианты: как в игре (−1), затем Top … 0
                int options = kinds[k].Top + 2;
                for (int opt = 0; opt < options; opt++)
                {
                    int value = opt == 0 ? SummonData.Vanilla : kinds[k].Top - (opt - 1);
                    float w = Theme.Px(value == SummonData.Vanilla ? SumVanillaW : value == 0 ? SumOffW : SumNumW);
                    string text = value == SummonData.Vanilla ? L.T("Как в игре", "As in game") : SummonData.LimitText(value);
                    if (GUI.Button(new Rect(x, by, w, btnH), Theme.Caps(text), value == current ? Theme.TabActive : Theme.Tab) && value != current)
                    {
                        PresetStore.SetSummon(e, k, value);
                        status = Runner.Display(e.Boss) + ": " + kinds[k].Name + " — "
                            + (value == SummonData.Vanilla ? L.T("как в игре", "as in the game") : SummonData.LimitText(value)) + ".";
                    }
                    x += w + gap;
                }
                x += Theme.Px(SumGroupGap);
            }
        }
    }
}
