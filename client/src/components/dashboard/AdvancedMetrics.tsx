import React from 'react';
import { TrendingDown, Scale, Shield, LineChart, Activity } from 'lucide-react';

interface AdvancedMetricsProps {
    data: {
        downsideRisk: number;
        maxDrawdown: number;
        sharpeRatio: number;
        valueAtRisk95: number;
        annualizedVolatility: number;
    };
}

const formatLossPercentage = (value: number) => {
    const magnitude = Math.abs(value).toFixed(2);
    return magnitude === '0.00' ? '0.00%' : `-${magnitude}%`;
};

export const AdvancedMetrics: React.FC<AdvancedMetricsProps> = ({ data }) => {
    return (
        <div className="bg-gray-800 rounded-xl p-6 shadow-lg border border-gray-700">
            <div className="flex items-center gap-4 mb-6">
                <div className="p-2.5 bg-indigo-500/20 rounded-xl text-indigo-400">
                    <LineChart size={24} />
                </div>
                <div>
                    <h2 className="text-xl font-bold text-white">Advanced Metrics</h2>
                    <p className="text-gray-400 text-sm mt-0.5">Comprehensive risk and performance analytics</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Downside Risk */}
                <div className="bg-gray-900 p-5 rounded-xl flex items-start gap-4" title="Volatility of negative returns only - measures downside risk">
                    <div className="p-3 bg-purple-500/20 rounded-xl text-purple-400 shrink-0">
                        <TrendingDown size={24} />
                    </div>
                    <div>
                        <p className="text-gray-400 text-sm font-medium mb-1">Downside Risk</p>
                        <p className="text-white text-2xl font-bold">{data.downsideRisk.toFixed(2)}%</p>
                        <p className="text-gray-400 text-xs mt-1">Downside volatility only</p>
                    </div>
                </div>

                {/* Max Drawdown */}
                <div className="bg-gray-900 p-5 rounded-xl flex items-start gap-4" title="Largest peak-to-trough decline in the period">
                    <div className="p-3 bg-red-500/20 rounded-xl text-red-400 shrink-0">
                        <TrendingDown size={24} />
                    </div>
                    <div>
                        <p className="text-gray-400 text-sm font-medium mb-1">Max Drawdown</p>
                        <p className="text-red-400 text-2xl font-bold">{formatLossPercentage(data.maxDrawdown)}</p>
                        <p className="text-gray-400 text-xs mt-1">Worst-case decline</p>
                    </div>
                </div>

                {/* Sharpe Ratio */}
                <div className="bg-gray-900 p-5 rounded-xl flex items-start gap-4" title="Risk-adjusted return metric - higher is better">
                    <div className="p-3 bg-emerald-500/20 rounded-xl text-emerald-400 shrink-0">
                        <Scale size={24} />
                    </div>
                    <div>
                        <p className="text-gray-400 text-sm font-medium mb-1">Sharpe Ratio</p>
                        <p className={`text-2xl font-bold ${data.sharpeRatio >= 1 ? 'text-emerald-400' :
                            data.sharpeRatio >= 0 ? 'text-yellow-400' :
                                'text-red-400'
                            }`}>
                            {data.sharpeRatio.toFixed(2)}
                        </p>
                        <p className="text-gray-400 text-xs mt-1">Risk-adjusted return</p>
                    </div>
                </div>

                {/* VaR 95% */}
                <div className="bg-gray-900 p-5 rounded-xl flex items-start gap-4" title="95% confidence worst-case loss">
                    <div className="p-3 bg-orange-500/20 rounded-xl text-orange-400 shrink-0">
                        <Shield size={24} />
                    </div>
                    <div>
                        <p className="text-gray-400 text-sm font-medium mb-1">VaR (95%)</p>
                        <p className="text-orange-400 text-2xl font-bold">{formatLossPercentage(data.valueAtRisk95)}</p>
                        <p className="text-gray-400 text-xs mt-1">95% confidence loss</p>
                    </div>
                </div>

                {/* Annualized Volatility */}
                <div className="bg-gray-900 p-5 rounded-xl flex items-start gap-4 col-span-1 md:col-span-2" title="Standard deviation annualized">
                    <div className="p-3 bg-blue-500/20 rounded-xl text-blue-400 shrink-0">
                        <Activity size={24} />
                    </div>
                    <div>
                        <p className="text-gray-400 text-sm font-medium mb-1">Annualized Volatility</p>
                        <p className="text-blue-400 text-2xl font-bold">{data.annualizedVolatility.toFixed(2)}%</p>
                        <p className="text-gray-400 text-xs mt-1">Historical price volatility</p>
                    </div>
                </div>
            </div>
        </div>
    );
};
