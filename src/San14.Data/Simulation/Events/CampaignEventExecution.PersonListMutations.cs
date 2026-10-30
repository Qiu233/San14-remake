using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public static partial class CampaignEventExecution
{
    internal static bool IsStoryWorldMutation(int code) => code is 366 or 151 or 320 or 443 or 352 or 349 or 350 or 356 or 302 or 303 or 304 or 301 or 137 or 138 or 284 or 439 or 368 or 384 or 112 or 113 or 114 or 382 or 141 or 150 or 251 or 170 or 254 or 330 or 380 or 445 or >= 232 and <= 238 or >= 142 and <= 148 or >= 273 and <= 279 or 346 or >= 122 and <= 126 or 428 or 430 or 166 or 377 or 426 or 447 or 448 or 152 or 247 or 365 or 168 or 259 or 260 or 449 or 450 or 209 or 210 or >= 305 and <= 310 or 312 or 383 or 121 or 115 or 446 or 120 or 263 or 267 or 289 or 158 or 159 or 160 or 153 or 154 or 155 or 156 or 162 or 245 or 252 or 285 or 294 or 295 or 149 or 299 or >= 184 and <= 187 or >= 193 and <= 202 or 116 or 117 or 118 or 119 or >= 127 and <= 131 or 163 or 270 or 256 or 287 or 171 or 359 or 351 or 323 or 286 or 211 or 212 or 213 or 164 or 165 or 319 or 315 or 248;
}

internal sealed partial class CampaignEventExecutionContext
{
    internal CampaignStoryPersonListContinuation? PersonListContinuation { get; set; }
    internal CampaignStoryForceRankContinuation? ForceRankContinuation { get; set; }
    internal CampaignStorySinglePersonDeathContinuation? SinglePersonDeathContinuation { get; set; }

    internal void ReplaceOrderedPeople(IReadOnlyList<int> people)
    {
        _candidates.Clear();
        _candidates.AddRange(people.Select(person => new S14EventConditionPersonReference(person)));
    }
}
